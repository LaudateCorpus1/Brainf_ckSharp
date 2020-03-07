﻿using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Brainf_ckSharp.Constants;
using Brainf_ckSharp.Extensions.Types;
using Brainf_ckSharp.Models;
using Brainf_ckSharp.Models.Internal;
using StackFrame = Brainf_ckSharp.Models.Internal.StackFrame;

#nullable enable

namespace Brainf_ckSharp
{
    public static partial class Brainf_ckInterpreter
    {
        /// <summary>
        /// A <see langword="class"/> implementing interpreter methods for the DEBUG configuration
        /// </summary>
        internal static partial class Debug
        {
            /// <summary>
            /// Loads the function definitions with the given executable and parameters
            /// </summary>
            /// <param name="operators">The sequence of parsed operators to execute</param>
            /// <param name="functions">The mapping of functions for the current execution</param>
            /// <param name="definitions">The lookup table to check which functions are defined</param>
            /// <param name="totalFunctions">The total number of defined functions</param>
            /// <returns>An array of <see cref="FunctionDefinition"/> instance with the defined functions</returns>
            [Pure]
            public static FunctionDefinition[] LoadFunctionDefinitions(
                UnmanagedSpan<byte> operators,
                UnmanagedSpan<Range> functions,
                UnmanagedSpan<ushort> definitions,
                int totalFunctions)
            {
                DebugGuard.MustBeGreaterThanOrEqualTo(operators.Size, 0, nameof(operators));
                DebugGuard.MustBeEqualTo(functions.Size, ushort.MaxValue, nameof(functions));
                DebugGuard.MustBeGreaterThanOrEqualTo(definitions.Size, 0, nameof(definitions));
                DebugGuard.MustBeLessThanOrEqualTo(definitions.Size, operators.Size / 3, nameof(definitions));
                DebugGuard.MustBeGreaterThanOrEqualTo(totalFunctions, 0, nameof(totalFunctions));

                // No declared functions
                if (totalFunctions == 0) return Array.Empty<FunctionDefinition>();

                FunctionDefinition[] result = new FunctionDefinition[totalFunctions];
                ref FunctionDefinition r0 = ref result[0];

                // Process all the declared functions
                for (int i = 0; i < totalFunctions; i++)
                {
                    ushort key = definitions[i];
                    Range range = functions[key];
                    int offset = range.Start - 1; // The range starts at the first function operator

                    UnmanagedSpan<byte> memory = operators.Slice(in range);
                    string body = Brainf_ckParser.Debug.ExtractSource(memory);

                    Unsafe.Add(ref r0, i) = new FunctionDefinition(key, i, offset, body);
                }

                return result;
            }

            /// <summary>
            /// Loads the jump table for loops and functions from a given executable
            /// </summary>
            /// <param name="operators">The sequence of parsed operators to inspect</param>
            /// <param name="functionsCount">The total number of declared functions in the input sequence of operators</param>
            /// <returns>The resulting precomputed jump table for the input executable</returns>
            [Pure]
            public static PinnedUnmanagedMemoryOwner<int> LoadJumpTable(
                PinnedUnmanagedMemoryOwner<byte> operators,
                out int functionsCount)
            {
                DebugGuard.MustBeGreaterThanOrEqualTo(operators.Size, 0, nameof(operators));

                PinnedUnmanagedMemoryOwner<int> jumpTable = PinnedUnmanagedMemoryOwner<int>.Allocate(operators.Size, false);

                /* Temporarily allocate two buffers to store the indirect indices to build the jump table.
                 * The two temporary buffers are initialized with a size of half the length of the input
                 * executable, because that is the maximum number of open square brackets in a valid source file.
                 * The two temporary buffers are used to implement an indirect indexing system while building
                 * the table, which allows to reduce the complexity of the operation from O(N^2) to O(N).
                 * UnsafeSpan<T> is used directly here instead of UnsafeMemoryBuffer<T> to save the cost of
                 * allocating a GCHandle and pinning each temporary buffer, which is not necessary since
                 * both buffers are only used in the scope of this method. */
                int tempBuffersLength = operators.Size / 2 + 1;
                using StackOnlyUnmanagedMemoryOwner<int> rootTempIndices = StackOnlyUnmanagedMemoryOwner<int>.Allocate(tempBuffersLength);
                using StackOnlyUnmanagedMemoryOwner<int> functionTempIndices = StackOnlyUnmanagedMemoryOwner<int>.Allocate(tempBuffersLength);
                ref int rootTempIndicesRef = ref rootTempIndices.GetReference();
                ref int functionTempIndicesRef = ref functionTempIndices.GetReference();
                functionsCount = 0;

                // Go through the executable to build the jump table for each open parenthesis or square bracket
                for (int r = 0, f = -1, i = 0; i < operators.Size; i++)
                {
                    switch (operators[i])
                    {
                        /* When a loop start, the current index is stored in the right
                         * temporary buffer, depending on whether or not the current
                         * part of the executable is within a function definition */
                        case Operators.LoopStart:
                            if (f == -1) Unsafe.Add(ref rootTempIndicesRef, r++) = i;
                            else Unsafe.Add(ref functionTempIndicesRef, f++) = i;
                            break;

                        /* When a loop ends, the index of the corresponding open square
                         * bracket is retrieved from the right temporary buffer, and the
                         * current index is stored at that location in the final jump table
                         * being built. The inverse mapping is stored too, so that each
                         * closing square bracket can reference the corresponding open
                         * bracket at the start of the loop. */
                        case Operators.LoopEnd:
                            int start = f == -1
                                ? Unsafe.Add(ref rootTempIndicesRef, --r)
                                : Unsafe.Add(ref functionTempIndicesRef, --f);
                            jumpTable[start] = i;
                            jumpTable[i] = start;
                            break;

                        /* When a function definition starts, the offset into the
                         * temporary buffer for the function indices is set to 1.
                         * This is because in this case a 1-based indexing is used:
                         * the first location in the temporary buffer is used to store
                         * the index of the open parenthesis for the function definition. */
                        case Operators.FunctionStart:
                            f = 1;
                            functionTempIndicesRef = i;
                            functionsCount++;
                            break;
                        case Operators.FunctionEnd:
                            f = -1;
                            jumpTable[functionTempIndicesRef] = i;
                            jumpTable[i] = functionTempIndicesRef;
                            break;
                    }
                }

                return jumpTable;
            }

            /// <summary>
            /// Loads the <see cref="HaltedExecutionInfo"/> instance for a halted execution of a script, if available
            /// </summary>
            /// <param name="operators">The sequence of parsed operators to execute</param>
            /// <param name="stackFrames">The sequence of stack frames for the current execution</param>
            /// <param name="depth">The current stack depth</param>
            /// <returns>An <see cref="HaltedExecutionInfo"/> instance, if the input script was halted during its execution</returns>
            [Pure]
            public static HaltedExecutionInfo? LoadDebugInfo(
                UnmanagedSpan<byte> operators,
                UnmanagedSpan<StackFrame> stackFrames,
                int depth)
            {
                DebugGuard.MustBeTrue(operators.Size > 0, nameof(operators));
                DebugGuard.MustBeEqualTo(stackFrames.Size, Specs.MaximumStackSize, nameof(stackFrames));
                DebugGuard.MustBeGreaterThanOrEqualTo(depth, -1, nameof(depth));

                // No exception info for scripts completed successfully
                if (depth == -1) return null;

                string[] stackTrace = new string[depth + 1];
                ref string r0 = ref stackTrace[0];

                // Process all the stack frames
                for (int i = 0, j = depth; j >= 0; i++, j--)
                {
                    StackFrame frame = stackFrames[j];

                    /* Adjust the offset and process the current range.
                     * This is needed because in case of a partial execution, no matter
                     * if it's a breakpoint or a crash, the stored offset in the top stack
                     * frame will be the operator currently being executed, which needs to
                     * be included in the processed string. For stack frames below that
                     * instead, the offset already refers to the operator immediately after
                     * the function call operator, so the offset doesn't need to be shifted
                     * ahead before extracting the processed string. Doing this with a
                     * reinterpret cast saves a conditional jump in the asm code. */
                    bool zero = i == 0;
                    int offset = frame.Offset + Unsafe.As<bool, byte>(ref zero);
                    UnmanagedSpan<byte> memory = operators.Slice(frame.Range.Start, offset);
                    string body = Brainf_ckParser.Debug.ExtractSource(memory);

                    Unsafe.Add(ref r0, i) = body;
                }

                // Extract the additional info
                int errorOffset = stackFrames[depth].Offset;
                char opcode = Brainf_ckParser.GetCharacterFromOperator(operators[errorOffset]);

                return new HaltedExecutionInfo(
                    stackTrace,
                    opcode,
                    errorOffset);
            }
        }

        /// <summary>
        /// A <see langword="class"/> implementing interpreter methods for the RELEASE configuration
        /// </summary>
        internal static class Release
        {
            /// <summary>
            /// Loads the function definitions with the given executable and parameters
            /// </summary>
            /// <param name="operations">The sequence of parsed operations to execute</param>
            /// <param name="functions">The mapping of functions for the current execution</param>
            /// <param name="definitions">The lookup table to check which functions are defined</param>
            /// <param name="totalFunctions">The total number of defined functions</param>
            /// <returns>An array of <see cref="FunctionDefinition"/> instance with the defined functions</returns>
            [Pure]
            public static FunctionDefinition[] LoadFunctionDefinitions(
                UnmanagedSpan<Brainf_ckOperation> operations,
                UnmanagedSpan<Range> functions,
                UnmanagedSpan<ushort> definitions,
                int totalFunctions)
            {
                DebugGuard.MustBeGreaterThanOrEqualTo(operations.Size, 0, nameof(operations));
                DebugGuard.MustBeEqualTo(functions.Size, ushort.MaxValue, nameof(functions));
                DebugGuard.MustBeGreaterThanOrEqualTo(definitions.Size, 0, nameof(definitions));
                DebugGuard.MustBeLessThanOrEqualTo(definitions.Size, operations.Size / 3, nameof(definitions));
                DebugGuard.MustBeGreaterThanOrEqualTo(totalFunctions, 0, nameof(totalFunctions));

                // No declared functions
                if (totalFunctions == 0) return Array.Empty<FunctionDefinition>();

                FunctionDefinition[] result = new FunctionDefinition[totalFunctions];
                ref FunctionDefinition r0 = ref result[0];

                // Process all the declared functions
                for (int i = 0; i < totalFunctions; i++)
                {
                    ushort key = definitions[i];
                    Range range = functions[key];
                    int offset = range.Start - 1; // The range starts at the first function operator

                    UnmanagedSpan<Brainf_ckOperation> memory = operations.Slice(in range);
                    string body = Brainf_ckParser.Release.ExtractSource(memory);

                    Unsafe.Add(ref r0, i) = new FunctionDefinition(key, i, offset, body);
                }

                return result;
            }

            /// <summary>
            /// Loads the jump table for loops and functions from a given executable
            /// </summary>
            /// <param name="operations">The sequence of parsed operations to inspect</param>
            /// <param name="functionsCount">The total number of declared functions in the input sequence of operations</param>
            /// <returns>The resulting precomputed jump table for the input executable</returns>
            [Pure]
            public static PinnedUnmanagedMemoryOwner<int> LoadJumpTable(
                PinnedUnmanagedMemoryOwner<Brainf_ckOperation> operations,
                out int functionsCount)
            {
                DebugGuard.MustBeGreaterThanOrEqualTo(operations.Size, 0, nameof(operations));

                PinnedUnmanagedMemoryOwner<int> jumpTable = PinnedUnmanagedMemoryOwner<int>.Allocate(operations.Size, false);

                /* Temporarily allocate two buffers to store the indirect indices to build the jump table.
                 * The two temporary buffers are initialized with a size of half the length of the input
                 * executable, because that is the maximum number of open square brackets in a valid source file.
                 * The two temporary buffers are used to implement an indirect indexing system while building
                 * the table, which allows to reduce the complexity of the operation from O(N^2) to O(N).
                 * UnsafeSpan<T> is used directly here instead of UnsafeMemoryBuffer<T> to save the cost of
                 * allocating a GCHandle and pinning each temporary buffer, which is not necessary since
                 * both buffers are only used in the scope of this method. */
                int tempBuffersLength = operations.Size / 2 + 1;
                using StackOnlyUnmanagedMemoryOwner<int> rootTempIndices = StackOnlyUnmanagedMemoryOwner<int>.Allocate(tempBuffersLength);
                using StackOnlyUnmanagedMemoryOwner<int> functionTempIndices = StackOnlyUnmanagedMemoryOwner<int>.Allocate(tempBuffersLength);
                ref int rootTempIndicesRef = ref rootTempIndices.GetReference();
                ref int functionTempIndicesRef = ref functionTempIndices.GetReference();
                functionsCount = 0;

                // Go through the executable to build the jump table for each open parenthesis or square bracket
                for (int r = 0, f = -1, i = 0; i < operations.Size; i++)
                {
                    switch (operations[i].Operator)
                    {
                        /* When a loop start, the current index is stored in the right
                         * temporary buffer, depending on whether or not the current
                         * part of the executable is within a function definition */
                        case Operators.LoopStart:
                            if (f == -1) Unsafe.Add(ref rootTempIndicesRef, r++) = i;
                            else Unsafe.Add(ref functionTempIndicesRef, f++) = i;
                            break;

                        /* When a loop ends, the index of the corresponding open square
                         * bracket is retrieved from the right temporary buffer, and the
                         * current index is stored at that location in the final jump table
                         * being built. The inverse mapping is stored too, so that each
                         * closing square bracket can reference the corresponding open
                         * bracket at the start of the loop. */
                        case Operators.LoopEnd:
                            int start = f == -1
                                ? Unsafe.Add(ref rootTempIndicesRef, --r)
                                : Unsafe.Add(ref functionTempIndicesRef, --f);
                            jumpTable[start] = i;
                            jumpTable[i] = start;
                            break;

                        /* When a function definition starts, the offset into the
                         * temporary buffer for the function indices is set to 1.
                         * This is because in this case a 1-based indexing is used:
                         * the first location in the temporary buffer is used to store
                         * the index of the open parenthesis for the function definition. */
                        case Operators.FunctionStart:
                            f = 1;
                            functionTempIndicesRef = i;
                            functionsCount++;
                            break;
                        case Operators.FunctionEnd:
                            f = -1;
                            jumpTable[functionTempIndicesRef] = i;
                            jumpTable[i] = functionTempIndicesRef;
                            break;
                    }
                }

                return jumpTable;
            }

            /// <summary>
            /// Loads the <see cref="HaltedExecutionInfo"/> instance for a halted execution of a script, if available
            /// </summary>
            /// <param name="operations">The sequence of parsed operations to execute</param>
            /// <param name="stackFrames">The sequence of stack frames for the current execution</param>
            /// <param name="depth">The current stack depth</param>
            /// <returns>An <see cref="HaltedExecutionInfo"/> instance, if the input script was halted during its execution</returns>
            [Pure]
            public static HaltedExecutionInfo? LoadDebugInfo(
                UnmanagedSpan<Brainf_ckOperation> operations,
                UnmanagedSpan<StackFrame> stackFrames,
                int depth)
            {
                DebugGuard.MustBeTrue(operations.Size > 0, nameof(operations));
                DebugGuard.MustBeEqualTo(stackFrames.Size, Specs.MaximumStackSize, nameof(stackFrames));
                DebugGuard.MustBeGreaterThanOrEqualTo(depth, -1, nameof(depth));

                // No exception info for scripts completed successfully
                if (depth == -1) return null;

                string[] stackTrace = new string[depth + 1];
                ref string r0 = ref stackTrace[0];

                // Process all the stack frames
                for (int i = 0, j = depth; j >= 0; i++, j--)
                {
                    StackFrame frame = stackFrames[j];

                    /* Adjust the offset and process the current range.
                     * This is needed because in case of a partial execution, no matter
                     * if it's a breakpoint or a crash, the stored offset in the top stack
                     * frame will be the operator currently being executed, which needs to
                     * be included in the processed string. For stack frames below that
                     * instead, the offset already refers to the operator immediately after
                     * the function call operator, so the offset doesn't need to be shifted
                     * ahead before extracting the processed string. Doing this with a
                     * reinterpret cast saves a conditional jump in the asm code. */
                    bool zero = i == 0;
                    int offset = frame.Offset + Unsafe.As<bool, byte>(ref zero);
                    UnmanagedSpan<Brainf_ckOperation> memory = operations.Slice(frame.Range.Start, offset);
                    string body = Brainf_ckParser.Release.ExtractSource(memory);

                    Unsafe.Add(ref r0, i) = body;
                }

                // Extract the additional info
                int errorOffset = stackFrames[depth].Offset;
                char opcode = Brainf_ckParser.GetCharacterFromOperator(operations[errorOffset].Operator);

                return new HaltedExecutionInfo(
                    stackTrace,
                    opcode,
                    errorOffset);
            }
        }
    }
}

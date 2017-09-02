﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Brainf_ck_sharp.Enums;
using Brainf_ck_sharp.Helpers;
using Brainf_ck_sharp.MemoryState;
using Brainf_ck_sharp.ReturnTypes;
using JetBrains.Annotations;

namespace Brainf_ck_sharp
{
    /// <summary>
    /// A simple class that handles all the Brainf_ck code and interprets it
    /// </summary>
    public static class Brainf_ckInterpreter
    {
        /// <summary>
        /// Gets the collection of valid Brainf_ck operators
        /// </summary>
        [NotNull]
        public static IReadOnlyCollection<char> Operators { get; } = new HashSet<char>(new[] { '+', '-', '>', '<', '.', ',', '[', ']' });

        /// <summary>
        /// Gets the default size of the available memory
        /// </summary>
        public const int DefaultMemorySize = 64;

        /// <summary>
        /// Gets the maximum allowed size for the output buffer
        /// </summary>
        public const int StdoutBufferSizeLimit = 1024;

        #region Public APIs

        /// <summary>
        /// Executes the given script and returns the result
        /// </summary>
        /// <param name="source">The source code with the script to execute</param>
        /// <param name="arguments">The arguments for the script</param>
        /// <param name="mode">Indicates the desired overflow mode for the script to run</param>
        /// <param name="size">The size of the memory to use to run the script</param>
        /// <param name="threshold">The optional time threshold to run the script</param>
        [PublicAPI]
        [Pure, NotNull]
        public static InterpreterResult Run([NotNull] String source, [NotNull] String arguments, OverflowMode mode = OverflowMode.ShortNoOverflow,
            int size = DefaultMemorySize, int? threshold = null)
        {
            return TryRun(source, arguments, new TouringMachineState(size), mode, threshold);
        }

        /// <summary>
        /// Executes the given script and returns the result
        /// </summary>
        /// <param name="source">The source code with the script to execute</param>
        /// <param name="arguments">The arguments for the script</param>
        /// <param name="state">The initial memory state to run the script</param>
        /// <param name="mode">Indicates the desired overflow mode for the script to run</param>
        /// <param name="threshold">The optional time threshold to run the script</param>
        [PublicAPI]
        [Pure, NotNull]
        public static InterpreterResult Run([NotNull] String source, [NotNull] String arguments,
            [NotNull] IReadonlyTouringMachineState state, OverflowMode mode = OverflowMode.ShortNoOverflow, int? threshold = null)
        {
            if (state is TouringMachineState touring)
            {
                return TryRun(source, arguments, touring.Clone(), mode, threshold);
            }
            throw new ArgumentException();
        }

        /// <summary>
        /// Initializes an execution session with the input source code
        /// </summary>
        /// <param name="source">The source code to use to initialize the session. A breakpoint will be added at the start of each code chunk after the first one</param>
        /// <param name="arguments">The optional arguments for the script</param>
        /// <param name="mode">Indicates the desired overflow mode for the script to run</param>
        /// <param name="size">The size of the memory state to use for the session</param>
        /// <param name="threshold">An optional time threshold for the execution of the whole session</param>
        [PublicAPI]
        [Pure, NotNull]
        public static InterpreterExecutionSession InitializeSession([NotNull] IReadOnlyList<String> source, [NotNull] String arguments,
            OverflowMode mode = OverflowMode.ShortNoOverflow, int size = DefaultMemorySize, int? threshold = null)
        {
            TouringMachineState state = new TouringMachineState(size);
            IReadOnlyList<IReadOnlyList<char>> chunks = source.Select(chunk => FindExecutableCode(chunk).ToArray()).ToArray();
            if (chunks.Count == 0 || chunks.Any(group => group.Count == 0))
            {
                return new InterpreterExecutionSession(
                    new InterpreterResult(InterpreterExitCode.Failure | InterpreterExitCode.NoCodeInterpreted, state,
                    TimeSpan.Zero, String.Empty, String.Empty, 0, null, null), null, mode);
            }

            List<Brainf_ckBinaryItem> executable = new List<Brainf_ckBinaryItem>();
            List<uint> breakpoints = new List<uint>();
            uint offset = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (i > 0) breakpoints.Add(offset);
                executable.AddRange(chunks[i].Select(c => new Brainf_ckBinaryItem(offset++, c)));
            }

            // Check the code syntax
            if (!CheckSourceSyntax(executable))
            {
                return new InterpreterExecutionSession(
                    new InterpreterResult(InterpreterExitCode.Failure | InterpreterExitCode.MismatchedParentheses, state,
                    TimeSpan.Zero, String.Empty, executable.Select(op => op.Operator).AggregateToString(), 0, null, null), null, mode);
            }

            // Prepare the input and output arguments
            Queue<char> input = arguments.Length > 0 ? new Queue<char>(arguments) : new Queue<char>();
            StringBuilder output = new StringBuilder();

            InterpreterResult result = TryRun(executable, input, output, state, mode, threshold, TimeSpan.Zero, 0, null, breakpoints.Count > 0 ? breakpoints : null);
            return new InterpreterExecutionSession(result, new SessionDebugData(executable, input, output, threshold, breakpoints), mode);
        }

        /// <summary>
        /// Checks whether or not the syntax in the input source code is valid
        /// </summary>
        /// <param name="source">The source code to analyze</param>
        /// <returns>A wrapper class that indicates whether or not the source code is valid, and the position of the first syntax error, if there is at least one</returns>
        [PublicAPI]
        [Pure]
        public static SyntaxValidationResult CheckSourceSyntax([NotNull] String source)
        {
            // Iterate over all the characters in the source
            int height = 0, error = 0;
            for (int i = 0; i < source.Length; i++)
            {
                // Check the parentheses
                if (source[i] == '[')
                {
                    if (height == 0) error = i;
                    height++;
                }
                else if (source[i] == ']')
                {
                    if (height == 0) return new SyntaxValidationResult(false, i);
                    height--;
                }
            }

            // Edge case or valid return
            return height == 0 ? new SyntaxValidationResult(true, 0) : new SyntaxValidationResult(false, error);
        }
        
        /// <summary>
        /// Checks whether or not the given source code contains at least one executable operator
        /// </summary>
        /// <param name="source">The source code to analyze</param>
        [PublicAPI]
        [Pure]
        public static bool FindOperators([NotNull] String source) => FindExecutableCode(source).Any();

        #endregion

        #region Interpreter implementation

        /// <summary>
        /// Executes the input script
        /// </summary>
        /// <param name="source">The source code with the script to execute</param>
        /// <param name="arguments">The arguments for the script</param>
        /// <param name="state">The initial memory state to run the script</param>
        /// <param name="mode">Indicates the desired overflow mode for the script to run</param>
        /// <param name="threshold">The optional time threshold to run the script</param>
        [Pure, NotNull]
        private static InterpreterResult TryRun([NotNull] String source, [NotNull] String arguments,
            [NotNull] TouringMachineState state, OverflowMode mode, int? threshold)
        {
            // Get the operators to execute and check if the source is empty
            IReadOnlyList<Brainf_ckBinaryItem> executable = FindExecutableCode(source).Select((c, i) => new Brainf_ckBinaryItem((uint)i, c)).ToArray();
            if (executable.Count == 0)
            {
                return new InterpreterResult(InterpreterExitCode.Failure | InterpreterExitCode.NoCodeInterpreted, state,
                    TimeSpan.Zero, String.Empty, String.Empty, 0, null, null);
            }

            // Check the code syntax
            if (!CheckSourceSyntax(executable))
            {
                return new InterpreterResult(InterpreterExitCode.Failure | InterpreterExitCode.MismatchedParentheses, state, TimeSpan.Zero, String.Empty,
                    executable.Select(op => op.Operator).AggregateToString(), 0, null, null);
            }

            // Prepare the input and output arguments
            Queue<char> input = arguments.Length > 0 ? new Queue<char>(arguments) : new Queue<char>();
            StringBuilder output = new StringBuilder();

            // Execute the code
            return TryRun(executable, input, output, state, mode, threshold, TimeSpan.Zero, 0, null, null);
        }

        /// <summary>
        /// Executes a script or continues the execution of a script
        /// </summary>
        /// <param name="executable">The source code of the scrpt to execute</param>
        /// <param name="input">The stdin buffer</param>
        /// <param name="output">The stdout buffer</param>
        /// <param name="state">The curret memory state to run or continue the script</param>
        /// <param name="mode">Indicates the desired overflow mode for the script to run</param>
        /// <param name="threshold">The optional time threshold for the execution of the script</param>
        /// <param name="elapsed">The elapsed time since the beginning of the script (if there's an execution session in progress)</param>
        /// <param name="operations">The number of previous operations executed in the current script, if it's being resumed</param>
        /// <param name="jump">The optional position of a previously reached breakpoint to use to resume the execution</param>
        /// <param name="breakpoints">The optional list of breakpoints in the input source code</param>
        [Pure, NotNull]
        private static InterpreterResult TryRun([NotNull] IReadOnlyList<Brainf_ckBinaryItem> executable, [NotNull] Queue<char> input, [NotNull] StringBuilder output,
            [NotNull] TouringMachineState state, OverflowMode mode, int? threshold, TimeSpan elapsed, uint operations, uint? jump, IReadOnlyList<uint> breakpoints)
        {
            // Preliminary tests
            if (executable.Count == 0) throw new ArgumentException("The source code can't be empty");
            if (threshold <= 0) throw new ArgumentOutOfRangeException("The threshold must be a positive value");
            if (jump < 0) throw new ArgumentOutOfRangeException("The target breakpoint position must be a positive number");
            if (jump.HasValue && (jump > executable.Count - 1 || breakpoints?.Contains(jump.Value) == false))
            {
                throw new ArgumentOutOfRangeException("The target breakpoint position isn't valid");
            }
            if (breakpoints?.Count == 0) throw new ArgumentException("The breakpoints list can't be empty");

            // Start the stopwatch to monitor the execution
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Internal recursive function that interpretes the code
            InterpreterWorkingData TryRunCore(IReadOnlyList<Brainf_ckBinaryItem> operators, uint depth, bool reached)
            {
                // Outer do-while that repeats the code if there's a loop
                bool repeat = false;
                uint partial = 0; // The number of executed operations in this call
                do
                {
                    // Check the current elapsed time
                    if (threshold.HasValue && timer.ElapsedMilliseconds > threshold.Value + elapsed.TotalMilliseconds)
                    {
                        return new InterpreterWorkingData(InterpreterExitCode.Failure | InterpreterExitCode.ThresholdExceeded, new[] { new char[0] }, depth, false, partial);
                    }

                    // Iterate over all the commands
                    int skip = 0;
                    for (int i = 0; i < operators.Count; i++)
                    {
                        // Skip the current character if inside a loop that points to a 0 cell
                        if (skip > 0)
                        {
                            skip--;
                            continue;
                        }

                        // Check the breakpoints if the current call isn't expected to go straight to the end of the script
                        if (jump == null && breakpoints?.Contains(operators[i].Offset) == true || // First breakpoint in the code
                            jump != null && breakpoints?.Contains(operators[i].Offset) == true && reached) // New breakpoint after restoring the execution
                        {
                            // First breakpoint in the current session
                            return new InterpreterWorkingData(InterpreterExitCode.Success |
                                                              InterpreterExitCode.BreakpointReached,
                                                              new[] { operators.Select(op => op.Operator).Take(i + 1) }, depth + (uint)i, true, partial);
                        }

                        // Keep track when the target breakpoint is reached and the previous execution is restored
                        if (jump == operators[i].Offset && !reached) reached = true;

                        // Parse the current operator
                        switch (operators[i].Operator)
                        {
                            // ptr++
                            case '>':
                                if (jump != null && !reached) continue;
                                if (state.CanMoveNext) state.MoveNext();
                                else return new InterpreterWorkingData(InterpreterExitCode.Failure |
                                                                       InterpreterExitCode.ExceptionThrown |
                                                                       InterpreterExitCode.UpperBoundExceeded,
                                                                       new[] { operators.Select(op => op.Operator).Take(i + 1) }, depth + (uint)i, reached, partial);
                                partial++;
                                break;

                            // ptr--
                            case '<':
                                if (jump != null && !reached) continue;
                                if (state.CanMoveBack) state.MoveBack();
                                else return new InterpreterWorkingData(InterpreterExitCode.Failure |
                                                                       InterpreterExitCode.ExceptionThrown |
                                                                       InterpreterExitCode.LowerBoundExceeded,
                                                                       new[] { operators.Select(op => op.Operator).Take(i + 1) }, depth + (uint)i, reached, partial);
                                partial++;
                                break;

                            // *ptr++
                            case '+':
                                if (jump != null && !reached) continue;
                                if (mode == OverflowMode.ByteOverflow && state.IsAtByteMax) state.Input((char)0);
                                else if (state.CanIncrement) state.Plus();
                                else return new InterpreterWorkingData(InterpreterExitCode.Failure |
                                                                       InterpreterExitCode.ExceptionThrown |
                                                                       InterpreterExitCode.MaxValueExceeded,
                                                                       new[] { operators.Select(op => op.Operator).Take(i + 1) }, depth + (uint)i, reached, partial);
                                partial++;
                                break;

                            // *ptr--
                            case '-':
                                if (jump != null && !reached) continue;
                                if (state.CanDecrement) state.Minus();
                                else if (mode == OverflowMode.ByteOverflow) state.Input((char)byte.MaxValue);
                                else return new InterpreterWorkingData(InterpreterExitCode.Failure |
                                                                       InterpreterExitCode.ExceptionThrown |
                                                                       InterpreterExitCode.NegativeValue,
                                                                       new[] { operators.Select(op => op.Operator).Take(i + 1) }, depth + (uint)i, reached, partial);
                                partial++;
                                break;

                            // while (*ptr) {
                            case '[':

                                // Edge case - memory reset loop [-]
                                if (state.Current.Value > 0 && (jump == null || jump != null && reached) &&
                                    i + 2 < operators.Count &&
                                    operators[i + 1].Operator == '-' && operators[i + 2].Operator == ']')
                                {
                                    // Check the position of the breakpoints
                                    uint offset = operators[i].Offset;
                                    if (breakpoints?.Any(b => b > offset && b <= offset + 2) != true)
                                    {
                                        partial += state.Current.Value * 2 + 1;
                                        state.ResetCell();
                                        skip = 2;
                                        break;
                                    }
                                }

                                // Extract the loop code and append the final ] character
                                IReadOnlyList<Brainf_ckBinaryItem> loop = ExtractInnerLoop(operators, i).ToArray();
                                skip = loop.Count;

                                // Execute the loop if the current value is greater than 0
                                if (state.Current.Value > 0 || jump != null && !reached)
                                {
                                    InterpreterWorkingData inner = TryRunCore(loop, depth + (uint)i + 1, reached);
                                    partial += inner.TotalOperations;
                                    if (!(jump != null && !reached)) partial++; // Only count the first [ if it's the first time it's evaluated
                                    reached |= inner.BreakpointReached;
                                    if ((inner.ExitCode & InterpreterExitCode.Success) == 0 ||
                                        (inner.ExitCode & InterpreterExitCode.BreakpointReached) == InterpreterExitCode.BreakpointReached)
                                    {
                                        return new InterpreterWorkingData(inner.ExitCode,
                                            inner.StackFrames.Concat(new[] { operators.Select(op => op.Operator).Take(i + 1) }), inner.Position, reached, partial);
                                    }
                                }
                                else if (state.Current.Value == 0) partial++;
                                break;

                            // }
                            case ']':
                                if (state.Current.Value == 0 || jump != null && !reached)
                                {
                                    // Loop end
                                    return new InterpreterWorkingData(InterpreterExitCode.Success, null, depth + (uint)i, reached,
                                        jump != null && !reached ? partial : partial + 1); // Increment the partial to include the closing ] bracket, if needed
                                }
                                else
                                {
                                    // Jump back and execute the loop body again
                                    repeat = true;
                                    partial++;
                                    continue;
                                }

                            // putch(*ptr)
                            case '.':
                                if (jump != null && !reached) continue;
                                if (output.Length >= StdoutBufferSizeLimit)
                                {
                                    return new InterpreterWorkingData(InterpreterExitCode.Failure |
                                                                      InterpreterExitCode.ExceptionThrown |
                                                                      InterpreterExitCode.StdoutBufferLimitExceeded,
                                                                      new[] { operators.Select(op => op.Operator).Take(i + 1) }, depth + (uint)i, reached, partial);
                                }
                                output.Append(state.Current.Character);
                                partial++;
                                break;

                            // *ptr = getch()
                            case ',':
                                if (jump != null && !reached) continue;
                                if (input.Count > 0)
                                {
                                    // Read the new character
                                    char c = input.Dequeue();
                                    if (mode == OverflowMode.ShortNoOverflow)
                                    {
                                        // Insert it if possible when the overflow is disabled
                                        if (c <= short.MaxValue) state.Input(c);
                                        else return new InterpreterWorkingData(InterpreterExitCode.Failure |
                                                                               InterpreterExitCode.ExceptionThrown |
                                                                               InterpreterExitCode.NegativeValue,
                                                                               new[] { operators.Select(op => op.Operator).Take(i + 1) }, depth + (uint)i, reached, partial);
                                    }
                                    else state.Input((char)(c % byte.MaxValue));
                                }
                                else return new InterpreterWorkingData(InterpreterExitCode.Failure |
                                                                       InterpreterExitCode.ExceptionThrown |
                                                                       InterpreterExitCode.StdinBufferExhausted,
                                                                       new[] { operators.Select(op => op.Operator).Take(i + 1) }, depth + (uint)i, reached, partial);
                                partial++;
                                break;
                        }
                    }
                } while (repeat);
                return new InterpreterWorkingData(InterpreterExitCode.Success, null, depth + (uint)operators.Count, reached, partial);
            }

            // Execute the code and stop the timer
            InterpreterWorkingData data = TryRunCore(executable, 0, false);
            timer.Stop();

            // Reconstruct the stack trace that generated the error
            IReadOnlyList<String> stackTrace = data.StackFrames == null
                ? null
                : (from frame in data.StackFrames
                   select frame.AggregateToString()).ToArray();

            // Return the interpreter result with all the necessary info
            String text = output.ToString();
            return new InterpreterResult(
                data.ExitCode | (text.Length > 0 ? InterpreterExitCode.TextOutput : InterpreterExitCode.NoOutput),
                state, timer.Elapsed.Add(elapsed), text, executable.Select(op => op.Operator).AggregateToString(), operations + data.TotalOperations,
                stackTrace, (data.ExitCode & InterpreterExitCode.BreakpointReached) == InterpreterExitCode.BreakpointReached ? (uint?)data.Position : null);
        }

        /// <summary>
        /// Extracts the body of a loop from the given source code partition (including the last ] operator in the loop body)
        /// </summary>
        /// <param name="source">The source code to use to extract the loop body</param>
        /// <param name="index">The index of the [ operator at the beginning of the loop to extract</param>
        [Pure, NotNull]
        private static IEnumerable<Brainf_ckBinaryItem> ExtractInnerLoop([NotNull] IReadOnlyList<Brainf_ckBinaryItem> source, int index)
        {
            // Initial checks
            if (source.Count == 0) throw new ArgumentException("The source code is empty");
            if (index < 0 || index > source.Count - 2) throw new ArgumentOutOfRangeException("The target index is invalid");
            if (source[index].Operator != '[') throw new ArgumentException("The target index doesn't point to the beginning of a loop");

            // Iterate from the first character of the loop to the final ] operator
            int height = 0;
            for (int i = index + 1; i < source.Count; i++)
            {
                if (source[i].Operator == '[') height++;
                else if (source[i].Operator == ']')
                {
                    if (height == 0) return source.Skip(index + 1).Take(i - index);
                    height--;
                }
            }
            throw new ArgumentException("The source code doesn't contain a well formatted nested loop at the given position");
        }

        #endregion

        #region Tools

        /// <summary>
        /// Continues the input execution session to its next step
        /// </summary>
        /// <param name="session">The session to continue</param>
        [Pure, NotNull]
        internal static InterpreterExecutionSession ContinueSession([NotNull] InterpreterExecutionSession session)
        {
            InterpreterResult step = TryRun(session.DebugData.Source, session.DebugData.Stdin, session.DebugData.Stdout,
                (TouringMachineState)session.CurrentResult.MachineState, session.Mode, session.DebugData.Threshold, session.CurrentResult.ElapsedTime, 
                session.CurrentResult.TotalOperations, session.CurrentResult.BreakpointPosition, session.DebugData.Breakpoints);
            return new InterpreterExecutionSession(step, session.DebugData, session.Mode);
        }

        /// <summary>
        /// Resumes the execution of the input session and runs it to the end
        /// </summary>
        /// <param name="session">The session to run</param>
        [Pure, NotNull]
        internal static InterpreterExecutionSession RunSessionToCompletion([NotNull] InterpreterExecutionSession session)
        {
            InterpreterResult step = TryRun(session.DebugData.Source, session.DebugData.Stdin, session.DebugData.Stdout,
                (TouringMachineState)session.CurrentResult.MachineState, session.Mode, session.DebugData.Threshold, session.CurrentResult.ElapsedTime, 
                session.CurrentResult.TotalOperations, session.CurrentResult.BreakpointPosition, null);
            return new InterpreterExecutionSession(step, session.DebugData, session.Mode);
        }

        /// <summary>
        /// Extracts the valid operators from a raw source code
        /// </summary>
        /// <param name="source">The input source code</param>
        [NotNull, LinqTunnel]
        private static IEnumerable<char> FindExecutableCode([NotNull] String source) => from c in source
                                                                                        where Operators.Contains(c)
                                                                                        select c;

        /// <summary>
        /// Checks whether or not the syntax in the input operators is valid
        /// </summary>
        /// <param name="operators">The operators sequence</param>
        [Pure]
        private static bool CheckSourceSyntax([NotNull] IEnumerable<Brainf_ckBinaryItem> operators)
        {
            // Iterate over all the characters in the source
            int height = 0;
            foreach (char c in operators.Select(op => op.Operator))
            {
                // Check the parentheses
                if (c == '[') height++;
                else if (c == ']')
                {
                    if (height == 0) return false;
                    height--;
                }
            }
            return height == 0;
        }

        #endregion

        #region C translator

        /// <summary>
        /// Translates the input source code into its C equivalent
        /// </summary>
        /// <param name="source">The source code with the script to translate</param>
        /// <param name="size">The size of the memory to use in the resulting code</param>
        [PublicAPI]
        [Pure, NotNull]
        public static String TranslateToC([NotNull] String source, int size = DefaultMemorySize)
        {
            // Arguments check
            if (size <= 0) throw new ArgumentOutOfRangeException("The input size is not valid");
            SyntaxValidationResult validationResult = CheckSourceSyntax(source);
            if (!validationResult.Valid) throw new ArgumentException("The input source code isn't valid");

            // Get the operators sequence and initialize the builder
            source = Regex.Replace(source, ",{2,}", "."); // Optimize repeated , operators with a single operator
            IReadOnlyList<char> executable = FindExecutableCode(source).ToArray();
            StringBuilder builder = new StringBuilder();

            // Prepare the header
            builder.Append($"#include <stdio.h>\n\nint main() {{\n\tchar array[{size}] = {{ 0 }};\n\tchar* ptr = array;\n");

            // Local function to get the right tabs for each indented line
            int depth = 1;
            String GetTabs(int count)
            {
                StringBuilder tabBuilder = new StringBuilder();
                while (count-- > 0) tabBuilder.Append('\t');
                return tabBuilder.ToString();
            }

            // Convert the source
            foreach (char c in executable)
            {
                switch (c)
                {
                    case '>':
                        builder.Append($"{GetTabs(depth)}++ptr;\n");
                        break;
                    case '<':
                        builder.Append($"{GetTabs(depth)}--ptr;\n");
                        break;
                    case '+':
                        builder.Append($"{GetTabs(depth)}(*ptr)++;\n");
                        break;
                    case '-':
                        builder.Append($"{GetTabs(depth)}(*ptr)--;\n");
                        break;
                    case '.':
                        builder.Append($"{GetTabs(depth)}putchar(*ptr);\n");
                        break;
                    case ',':
                        builder.Append($"{GetTabs(depth)}while ((*ptr=getchar()) == '\\n') {{ }};\n");
                        break;
                    case '[':
                        builder.Append($"{GetTabs(depth++)}while (*ptr) {{\n");
                        break;
                    case ']':
                        builder.Append($"{GetTabs(--depth)}}}\n");
                        break;
                }
            }

            // Add the final statement and return the translated source
            builder.Append("\treturn 0;\n}");
            return builder.ToString();
        }

        #endregion
    }
}

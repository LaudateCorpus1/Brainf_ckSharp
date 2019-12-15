﻿using System;

namespace Brainf_ck_sharp.NET.Enums
{
    /// <summary>
    /// An <see langword="enum"/> that indicates the exit code for an interpreted Brainf*ck/PBrain script
    /// </summary>
    [Flags]
    public enum ExitCode : uint
    {
        /// <summary>
        /// The code was interpreted successfully
        /// </summary>
        Success = 1,

        /// <summary>
        /// There were issues in the code that prevented it from being run successfully
        /// </summary>
        Failure = 1 << 1,

        /// <summary>
        /// The code didn't produce any output
        /// </summary>
        NoOutput = 1 << 2 | Success,

        /// <summary>
        /// The code produced at least an output character
        /// </summary>
        TextOutput = 1 << 3 | Success,

        /// <summary>
        /// The input source code didn't contain any valid Branf_ck operators to interpret
        /// </summary>
        NoCodeInterpreted = 1 << 4 | Failure,

        /// <summary>
        /// The source code produced a runtime exception
        /// </summary>
        ExceptionThrown = 1 << 5 | Failure,

        /// <summary>
        /// The source code contained a syntax error and couldn't be interpreted
        /// </summary>
        SyntaxError = 1 << 6 | Failure,

        /// <summary>
        /// The code run into an infinite loop (according to the desired time threshold)
        /// </summary>
        ThresholdExceeded = 1 << 7 | Failure,

        /// <summary>
        /// An internal interpreter exception has been thrown and automatically handled
        /// </summary>
        InternalException = 1 << 8 | Failure,

        /// <summary>
        /// The script execution was halted after reaching a breakpoint
        /// </summary>
        BreakpointReached = 1 << 9 | Success,

        /// <summary>
        /// The script tried to move back from the first memory location
        /// </summary>
        LowerBoundExceeded = 1 << 10 | ExceptionThrown,

        /// <summary>
        /// The script tried to move over the last memory location
        /// </summary>
        UpperBoundExceeded = 1 << 11 | ExceptionThrown,

        /// <summary>
        /// The script tried to lower the value of a memory cell set to 0
        /// </summary>
        NegativeValue = 1 << 12 | ExceptionThrown,

        /// <summary>
        /// The script tried to increase the value of a memory cell that had the maximum allowed value
        /// </summary>
        MaxValueExceeded = 1 << 13 | ExceptionThrown,

        /// <summary>
        /// The script requested another input character when the available buffer was empty
        /// </summary>
        StdinBufferExhausted = 1 << 14 | ExceptionThrown,

        /// <summary>
        /// The script tried to print too many characters in the output buffer
        /// </summary>
        StdoutBufferLimitExceeded = 1 << 15 | ExceptionThrown,

        /// <summary>
        /// The script tried to reference an undefined function
        /// </summary>
        UndefinedFunctionCalled = 1 << 16 | ExceptionThrown,

        /// <summary>
        /// The script tried to define a function with a value that was already mapped to another function
        /// </summary>
        DuplicateFunctionDefinition = 1 << 17 | ExceptionThrown,

        /// <summary>
        /// The script tried to define the same function more than once
        /// </summary>
        FunctionAlreadyDefined = 1 << 18 | ExceptionThrown,

        /// <summary>
        /// The script executed one or more recursive functions too many times
        /// </summary>
        StackLimitExceeded = 1 << 19 | ExceptionThrown
    }
}
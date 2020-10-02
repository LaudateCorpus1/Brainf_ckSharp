﻿using System.Diagnostics.Contracts;
using Brainf_ckSharp.Constants;
using Brainf_ckSharp.Enums;
using Brainf_ckSharp.Memory.Interfaces;
using Microsoft.Toolkit.Diagnostics;

namespace Brainf_ckSharp.Memory.Tools
{
    /// <summary>
    /// A <see langword="class"/> that provides the ability to create empty machine states for later use
    /// </summary>
    public static class MachineStateProvider
    {
        /// <summary>
        /// Gets the default machine state instance
        /// </summary>
        public static IReadOnlyMachineState Default { get; } = Create(Specs.DefaultMemorySize, Specs.DefaultOverflowMode);

        /// <summary>
        /// Creates a new <see cref="IReadOnlyMachineState"/> instance with the specified parameters
        /// </summary>
        /// <param name="size">The size of the state machine to create</param>
        /// <returns>A new <see cref="IReadOnlyMachineState"/> instance with the specified parameters</returns>
        [Pure]
        public static IReadOnlyMachineState Create(int size) => Create(size, Specs.DefaultOverflowMode);

        /// <summary>
        /// Creates a new <see cref="IReadOnlyMachineState"/> instance with the specified parameters
        /// </summary>
        /// <param name="size">The size of the state machine to create</param>
        /// <param name="overflowMode">The overflow mode to use in the state machine to create</param>
        /// <returns>A new <see cref="IReadOnlyMachineState"/> instance with the specified parameters</returns>
        [Pure]
        public static IReadOnlyMachineState Create(int size, OverflowMode overflowMode)
        {
            Guard.IsBetweenOrEqualTo(size, 32, 1024, nameof(size));

            return overflowMode switch
            {
                OverflowMode.ByteWithOverflow => new TuringMachineState<byte>(size, overflowMode),
                OverflowMode.ByteWithNoOverflow => new TuringMachineState<byte>(size, overflowMode),
                OverflowMode.UshortWithOverflow => new TuringMachineState<ushort>(size, overflowMode),
                OverflowMode.UshortWithNoOverflow => new TuringMachineState<ushort>(size, overflowMode),
                _ => ThrowHelper.ThrowArgumentOutOfRangeException<IReadOnlyMachineState>(nameof(overflowMode), "Invalid execution mode")
            };
        }
    }
}

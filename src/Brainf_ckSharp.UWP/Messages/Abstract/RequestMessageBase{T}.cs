﻿using System;

namespace Brainf_ckSharp.UWP.Messages.Abstract
{
    /// <summary>
    /// A base <see langword="class"/> for request message
    /// </summary>
    /// <typeparam name="T">The type of value to request</typeparam>
    public abstract class RequestMessageBase<T>
    {
        /// <summary>
        /// Gets the message response
        /// </summary>
        public T Result { get; private set; }

        /// <summary>
        /// Gets whether or not a result has already been assigned to this instance
        /// </summary>
        public bool ResponseReceived { get; private set; }

        /// <summary>
        /// Reports a result for the current request message
        /// </summary>
        public void ReportResult(T result)
        {
            if (ResponseReceived) throw new InvalidOperationException("This message has already been used");

            ResponseReceived = true;
            Result = result;
        }
    }
}

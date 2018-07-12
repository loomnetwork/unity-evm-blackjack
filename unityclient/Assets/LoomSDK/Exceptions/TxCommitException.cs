using System;

namespace Loom.Unity3d
{
    /// <summary>
    /// Used to signal that commiting a transaction has failed.
    /// </summary>
    public class TxCommitException : LoomException
    {
        private readonly int code;
        private readonly string error;

        public override string Message => $"[Code {this.code}] {this.error}";

        public TxCommitException(int code, string error)
        {
            this.code = code;
            this.error = error ?? "";
        }
    }
}
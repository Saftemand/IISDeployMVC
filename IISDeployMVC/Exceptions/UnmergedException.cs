using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IISDeployMVC.Exceptions
{
    public class UnmergedException : Exception
    {
        public UnmergedException() { }

        public UnmergedException(string message) : base(message){ }

        public UnmergedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
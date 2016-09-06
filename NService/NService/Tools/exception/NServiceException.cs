using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NService
{
    public class NSErrorException : Exception
    {
        #region Standard constructors - do not use

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        public NSErrorException()
        {

        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        public NSErrorException(string message) : base(message)
        {
        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        /// <param name="innerException">Inner exception.</param>
        public NSErrorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }

    public class NSInfoException : Exception
    {
        #region Standard constructors - do not use

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        public NSInfoException()
        {

        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        public NSInfoException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        /// <param name="innerException">Inner exception.</param>
        public NSInfoException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }
}

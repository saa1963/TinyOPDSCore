using System;
using System.Collections.Generic;
using System.Text;

namespace TinyOPDSCore.Data
{
    public static class LibraryFactory
    {
        private static Object thisLock = new Object();
        private static ILibrary _library = null;
        public static ILibrary GetLibrary()
        {
            lock (thisLock)
            {
                if (_library == null)
                {
                    if (Properties.LibraryKind == 0)
                        _library = new Library();
                    else if (Properties.LibraryKind == 1)
                        _library = new MyHomeLibrary();
                }
            }
            return _library;
        }
    }
}

using System;
using System.Linq;

namespace TinyOPDSCore.Misc
{
    /// <summary>
    /// Detects if we are running inside a unit test.
    /// </summary>
    public static class UnitTestDetector
    {
        static UnitTestDetector()
        {
            string testAssemblyName = "Microsoft.VisualStudio.TestPlatform";
            var ass = AppDomain.CurrentDomain.GetAssemblies();
            UnitTestDetector.IsInUnitTest = ass
                .Any(a => a.FullName.StartsWith(testAssemblyName));
        }

        public static bool IsInUnitTest { get; private set; }
    }
}

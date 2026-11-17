using Microsoft.VisualStudio.TestTools.UnitTesting;

// Tests that touch the shared database or config must not run in parallel.
[assembly: DoNotParallelize]

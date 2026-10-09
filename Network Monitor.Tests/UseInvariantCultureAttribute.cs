using System.Globalization;
using System.Reflection;
using System.Threading;
using Xunit.Sdk;

namespace Network_Monitor.Tests;

/// <summary>
/// Runs a test with the invariant culture, so number formatting doesn't depend on the machine's regional settings.
/// </summary>
public sealed class UseInvariantCultureAttribute : BeforeAfterTestAttribute
{
    private CultureInfo _originalCulture;

    public override void Before(MethodInfo methodUnderTest)
    {
        _originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public override void After(MethodInfo methodUnderTest)
    {
        Thread.CurrentThread.CurrentCulture = _originalCulture;
    }
}

using System;
using System.Linq;
namespace MeowSci.SkittlesLib;
public sealed partial class SkittlesSubmod
{
    public void ReleaseLiveState()
    {
        _themeManager?.RestoreDefaults();
    }
}

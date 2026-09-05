using System;
using System.Linq;
namespace MeowSci.ConManLib;
public sealed partial class ConManSubmod
{
    public void ReleaseLiveState()
    {
        _layoutManager?.RestoreOriginal();
    }
}

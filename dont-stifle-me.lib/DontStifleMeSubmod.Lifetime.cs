using System;
using System.Linq;
namespace MeowSci.DontStifleMeLib;
public sealed partial class DontStifleMeSubmod
{
    public void ReleaseLiveState()
    {
        ApplyPolicy(false, true, false);
    }
}

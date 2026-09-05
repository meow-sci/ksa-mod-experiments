using System;
using System.Linq;
namespace MeowSci.FreeFallinLib;
public sealed partial class FreeFallinSubmod
{
    public void ReleaseLiveState()
    {
        FreeFallinPatches.RestoreStock();
    }
}

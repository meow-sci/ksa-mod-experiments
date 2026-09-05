using System;
using System.Linq;
namespace MeowSci.KitchenSinkLib;
public sealed partial class KitchenSinkSubmod
{
    public void ReleaseLiveState()
    {
        IvaForceRender.Enabled = false;
    }
}

using System;
using System.Linq;
namespace MeowSci.BloominOnionLib;
public sealed partial class BloominOnionSubmod
{
    public void ReleaseLiveState()
    {
        if (!_controller.RemoveAll(out var message)) throw new InvalidOperationException(message);
    }
}

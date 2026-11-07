using System;
using System.Collections.Generic;
using System.Linq;

namespace BinaryBufferLib.Test;

class TestUtils
{
    public static IEnumerable<bool> EnumBool(bool? value = null)
    {
        if (value.HasValue)
            yield return value.Value;
        else
        {
            yield return false;
            yield return true;
        }
    }
}

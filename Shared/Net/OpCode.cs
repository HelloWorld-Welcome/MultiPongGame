using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Net
{
    public enum OpCode : byte
    {
        EnterRequest = 1,
        EnterResponse = 2,
        LeaveRequest = 3,
        LeaveResponse = 4,
        InputState = 10,
        UpdateSnapshot = 11,
        Ping = 250,
        Pong = 251, 
    }
}

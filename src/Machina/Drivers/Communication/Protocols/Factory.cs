using System;

namespace Machina.Drivers.Communication.Protocols;

internal static class Factory
{
    internal static Base GetTranslator(Driver driver)
    {
        return driver.parentControl.parentRobot.Brand switch
        {
            RobotType.ABB => new ABBCommunicationProtocol(),
            RobotType.UR => new URCommunicationProtocol(),
            RobotType.KUKA => new KUKACommunicationProtocol(),
            _ => throw new NotImplementedException(),
        };
    }
}

using System.Reflection;
namespace RoomArch;

public class SharedMethodInfo
{
    public readonly object ClassInstance;
    public readonly Type ValueType;
    public MethodInfo Method;

    public SharedMethodInfo(object classInstance, Type valueType, MethodInfo method)
    {
        ClassInstance = classInstance;
        ValueType = valueType;
        Method = method;
    }
}
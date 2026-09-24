#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    // Necesario para usar records e init en .NET Framework.
    internal static class IsExternalInit { }
}
#endif

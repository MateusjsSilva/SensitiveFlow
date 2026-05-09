// Polyfill required for 'record' and 'init' setters on netstandard2.0.
// The compiler uses this type internally; it is not part of the public API.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

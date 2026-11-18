namespace System.Runtime.CompilerServices
{
    // The compiler tags every coroutine (IEnumerator + yield) with this
    // attribute. It only exists in .NET 4.5+'s mscorlib, which Tattletail's
    // old Mono doesn't have — so any code reading those methods' attributes
    // (Harmony's PatchAll, Unity's AddComponent) threw a TypeLoadException
    // and could take the whole mod down. Defining it here makes the compiler
    // use this copy, which always loads.
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal sealed class IteratorStateMachineAttribute : Attribute
    {
        public IteratorStateMachineAttribute(Type stateMachineType) { StateMachineType = stateMachineType; }
        public Type StateMachineType { get; private set; }
    }
}

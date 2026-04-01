using System;

namespace IvoriesStudios.EnsureObjectInitialization
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RequiresInitializationAttribute : Attribute
    {
        public string InitializationMethodName { get; }

        public RequiresInitializationAttribute(string initializationMethodName)
        {
            InitializationMethodName = initializationMethodName;
        }
    }
}

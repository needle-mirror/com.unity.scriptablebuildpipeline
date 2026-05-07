using System;
using System.Reflection;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.Build.Pipeline.Injector
{
    /// <summary>
    /// When applied together with <see cref="InjectContextAttribute"/> on a field, context injection
    /// skips assigning from the build context if the field already holds a non-null value, so existing references are preserved.
    /// </summary>
    /// <remarks>
    /// Only meaningful for fields that participate in injection (for example, <see cref="ContextUsage.In"/> or <see cref="ContextUsage.InOut"/>).
    /// Extraction is unaffected. If the field is null, injection proceeds as usual.
    /// </remarks>
    /// <seealso cref="InjectContextAttribute"/>
    [AttributeUsage(AttributeTargets.Field)]
    public class BuildOverwriteProtectedAttribute : Attribute { }

    /// <summary>
    /// Use to pass around information between build tasks.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class InjectContextAttribute : Attribute
    {
        //public string Identifier { get; set; }
        /// <summary>
        /// Stores the how the attribute is used among build tasks.
        /// </summary>
        public ContextUsage Usage { get; set; }
        /// <summary>
        /// Stores whether using the context attribute is optional.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Creates a new context attribute that stores information that can be passed between build tasks.
        /// </summary>
        /// <param name="usage">The usage behavior for the attribute. By default it is set to <see cref="ContextUsage.InOut"/>.</param>
        /// <param name="optional">Set to true if using the attribute is optional. Set to false otherwise.</param>
        public InjectContextAttribute(ContextUsage usage = ContextUsage.InOut, bool optional = false)
        {
            this.Usage = usage;
            Optional = optional;
        }
    }

    /// <summary>
    /// Options for how the attribute is used among build tasks. It can be either injected to and or extracted from a build task.
    /// </summary>
    public enum ContextUsage
    {
        /// <summary>
        /// Use to indicate that the attribute can be injected to and extracted from a build task.
        /// </summary>
        InOut,
        /// <summary>
        /// Use to indicate that the attribute can only be injected to a build task.
        /// </summary>
        In,
        /// <summary>
        /// Use to indicate that the attribute can only be extracted from a build task.
        /// </summary>
        Out
    }

    class ContextInjector
    {
        public static void Inject(IBuildContext context, object obj)
        {
            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                object[] attrs = field.GetCustomAttributes(typeof(InjectContextAttribute), true);
                if (attrs.Length == 0)
                    continue;

                var overwriteProtection = field.IsDefined(typeof(BuildOverwriteProtectedAttribute), false);

                InjectContextAttribute attr = attrs[0] as InjectContextAttribute;
                if (attr == null || attr.Usage == ContextUsage.Out)
                    continue;

                object injectionObject;
                if (field.FieldType == typeof(IBuildContext))
                    injectionObject = context;
                else if (!attr.Optional)
                    injectionObject = context.GetContextObject(field.FieldType);
                else
                {
                    IContextObject contextObject;
                    context.TryGetContextObject(field.FieldType, out contextObject);
                    injectionObject = contextObject;
                }

                //If an object already has data and is marked as overwrite protected, skip injection, which would
                //overwrite existing data.
                if (overwriteProtection && field.GetValue(obj) != null)
                    continue;

                field.SetValue(obj, injectionObject);
            }
        }

        public static void Extract(IBuildContext context, object obj)
        {
            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                object[] attrs = field.GetCustomAttributes(typeof(InjectContextAttribute), true);
                if (attrs.Length == 0)
                    continue;

                InjectContextAttribute attr = attrs[0] as InjectContextAttribute;
                if (attr == null || attr.Usage == ContextUsage.In)
                    continue;

                if (field.FieldType == typeof(IBuildContext))
                    throw new InvalidOperationException("IBuildContext can only be used with the ContextUsage.In option.");

                IContextObject contextObject = field.GetValue(obj) as IContextObject;
                if (!attr.Optional)
                    context.SetContextObject(field.FieldType, contextObject);
                else if (contextObject != null)
                    context.SetContextObject(field.FieldType, contextObject);
            }
        }
    }
}

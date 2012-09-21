using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Burrow.RPC
{
    public interface IMethodFilter
    {
        bool IsAsync(MethodInfo method, Attribute[] attributes);
        void CheckValid(MethodInfo method, Attribute[] attributes, bool methodIsAsync);
    }

    internal class DefaultMethodFilter : IMethodFilter
    {
        internal static readonly ConcurrentDictionary<MethodInfo, Exception> CheckedMethodCaches = new ConcurrentDictionary<MethodInfo, Exception>();
        public bool IsAsync(MethodInfo method, Attribute[] attributes)
        {
            return attributes != null && attributes.Any(attribute => attribute is AsyncAttribute);
        }

        public void CheckValid(MethodInfo method, Attribute[] attributes, bool methodIsAsync)
        {
            if (!CheckedMethodCaches.ContainsKey(method))
            {
                var @params = method.GetParameters();

                var delegateParam = @params.FirstOrDefault(p => typeof (Delegate).IsAssignableFrom(p.ParameterType));
                if (delegateParam != null)
                {
                    CheckedMethodCaches[method] = new NotSupportedException(string.Format("Dude, method {0} has param {1} is a delegate of type {2}. RPC call does not support delegate", method.Name, delegateParam.Name, delegateParam.ParameterType.FullName));
                    
                }
                else if (!methodIsAsync)
                {
                    CheckedMethodCaches[method] = null;
                }

                else if (method.DeclaringType != null && method.DeclaringType.GetProperties().Any(prop => prop.GetSetMethod() == method || prop.GetGetMethod() == method))
                {
                    CheckedMethodCaches[method] = new NotSupportedException(string.Format("Property accessor {0} is not supported for RPC call", method.Name));
                }
                else if (method.ReturnType != typeof (void))
                {
                    CheckedMethodCaches[method] = new Exception(string.Format("Dude, method {0} requires to return value but it's expected to run asynchronously", method.Name));
                }

                else foreach (var param in @params)
                {
                    if (param.IsOut)
                    {
                        CheckedMethodCaches[method] = new Exception(string.Format("Dude, param '{0}' of method {1} is out param, but the method is expected to run asynchronously", param.Name, method.Name));
                        break;
                    }
                }
            }

            if (CheckedMethodCaches.ContainsKey(method) && CheckedMethodCaches[method] != null)
            {
                throw CheckedMethodCaches[method];
            }
        }
    }
}
using System;
using System.Threading.Tasks;
using LCU.StateAPI.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.WebJobs.Host.Protocols;
using System.Reflection;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    public class LCUStateAttribute : Attribute, IConnectionProvider
    {
        // Other properties ... 

        [AppSetting]
        public string Connection { get; set; }

        [AutoResolve]
        public string Text { get; set; }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    [Binding]
    public class LCUStateDetailsAttribute : Attribute
    {
    }

    public class LCUStateDetailsExtensionConfigProvider : IExtensionConfigProvider
    {
        #region Fields
        protected readonly HttpContextAccessor httpContextAccessor;
        #endregion

        #region Constructors
        public LCUStateDetailsExtensionConfigProvider(HttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }
        #endregion

        public void Initialize(ExtensionConfigContext context)
        {
            context.AddConverter<JObject, StateDetails>(input => input.ToObject<StateDetails>());

            context.AddConverter<StateDetails, string>(input => input.ToJSON());

            // Add a binding rule for Collector
            context.AddBindingRule<LCUStateDetailsAttribute>()
                .BindToInput<StateDetails>(handleInput);
        }

        protected virtual async Task<StateDetails> handleInput(LCUStateDetailsAttribute attr, ValueBindingContext valBinding)
        {
            var context = (HttpContext)valBinding.FunctionContext.InstanceServices.GetService(typeof(HttpContext));

            // var context = httpContextAccessor.HttpContext;

            var stateDetails = StateUtils.LoadStateDetails(context.Request, context.User);

            return stateDetails;
        }
    }

    public class HttpDirectRequestBindingProvider : IBindingProvider
    {
        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            if (parameter.ParameterType == typeof(HttpRequest))
            {
                // Not already claimed by another trigger?
                if (!HasBindingAttributes(parameter))
                {
                    return Task.FromResult<IBinding>(new LCUStateDetailsBinding());
                }
            }
            return Task.FromResult<IBinding>(null);
        }

        private static bool HasBindingAttributes(ParameterInfo parameter)
        {
            foreach (Attribute attr in parameter.GetCustomAttributes(false))
            {
                if (IsBindingAttribute(attr))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsBindingAttribute(Attribute attribute)
        {
            return attribute.GetType().GetCustomAttribute<BindingAttribute>() != null;
        }

    }
    
    public class LCUStateDetailsBinding : IBinding
    {
        public const string RequestBindingName = "$request";

        public bool FromAttribute
        {
            get
            {
                return false;
            }
        }

        public Task<IValueProvider> BindAsync(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var request = context.BindingData[RequestBindingName];

            return BindAsync(request, context.ValueContext);
        }

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            var request = value as HttpRequest;
            if (request != null)
            {
                var binding = new SimpleValueProvider(typeof(HttpRequest), request, "request");

                return Task.FromResult<IValueProvider>(binding);
            }
            throw new InvalidOperationException("value must be an HttpRequest");
        }

        public virtual ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor
            {
                Name = "request"
            };
        }
    }

    public class SimpleValueProvider : IValueProvider
    {
        private readonly Type _type;
        private readonly object _value;
        private readonly string _invokeString;

        public SimpleValueProvider(Type type, object value, string invokeString)
        {
            _type = type;
            _value = value;
            _invokeString = invokeString;
        }

        public Type Type
        {
            get
            {
                return _type;
            }
        }

        public Task<object> GetValueAsync()
        {
            return Task.FromResult(_value);
        }

        public string ToInvokeString()
        {
            return _invokeString;
        }
    }
}
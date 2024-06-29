using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace _4_Device
{
    internal unsafe class Program
    {
        private static IWindow _window;

        private static WebGPU _wgpu;
        private static Instance* _instance;
        private static Adapter* _adapter;
        private static Device* _device;
        static void Main(string[] args)
        {
            _window = Window.Create(WindowOptions.Default with{
                API = GraphicsAPI.None,
                WindowBorder = WindowBorder.Fixed
            });

            _window.Load += WindowLoad;
            _window.Render += WindowRender;
            _window.Update += WindowOnUpdate;
            _window.Closing += WindowClosing;

            _window.Initialize();
            _window.Center();
            _window.Run();
        }
        private static void WindowLoad()
        {
            _wgpu = WebGPU.GetApi(); 
          
            // create instance
            _instance = _wgpu.CreateInstance(new InstanceDescriptor());
            if (_instance is null)
                throw new Exception("Instance create failure");

            // request adapter
            var pfnRequestAdapterCallback=PfnRequestAdapterCallback.From((status, adapter, message, _) =>
            {
                if (status == RequestAdapterStatus.Success)
                    _adapter = adapter;
                else
                    throw new Exception(Marshal.PtrToStringUTF8((IntPtr)message));
            });
            _wgpu.InstanceRequestAdapter(_instance, new RequestAdapterOptions(), pfnRequestAdapterCallback, default);
            InspectAdapter();
            
            // request device
            var deviceDescriptor = new DeviceDescriptor
            {
                Label = (byte*)SilkMarshal.StringToPtr("MyDevice"),
                RequiredFeatureCount = 0,
                RequiredFeatures = null,
                RequiredLimits = null,
                DeviceLostCallback = PfnDeviceLostCallback.From(OnDeviceLostCallBack)
            };
            var pfnRequestDeviceCallback = PfnRequestDeviceCallback.From((status, device, message, _) =>
            {
                if (status == RequestDeviceStatus.Success)
                    _device = device;
                else
                    throw new Exception(Marshal.PtrToStringUTF8((IntPtr)message));
            });
            _wgpu.AdapterRequestDevice(_adapter, deviceDescriptor, pfnRequestDeviceCallback,null);
            var pfnErrorCallback = PfnErrorCallback.From((errorType, message, _) =>
                Console.WriteLine($"Error type:{errorType},{Marshal.PtrToStringUTF8((IntPtr)message)}"));
            _wgpu.DeviceSetUncapturedErrorCallback(_device, pfnErrorCallback, null);
            InspectDevice();


        }

        private static void WindowOnUpdate(double obj)
        {
           
        }



        private static void WindowRender(double obj)
        {
            
        }

        private static void WindowClosing()
        {
            _wgpu.InstanceRelease(_instance);
        }

        private static void InspectAdapter()
        {
            // get adapter limits
            Console.WriteLine("Adapter limits:");
            SupportedLimits supportedLimits = new SupportedLimits();
            if (_wgpu.AdapterGetLimits(_adapter, ref supportedLimits))
            {
                foreach (var fieldInfo in supportedLimits.Limits.GetType().GetFields())
                {
                    Console.WriteLine($"\t{fieldInfo.Name}: {fieldInfo.GetValue(supportedLimits.Limits)}");
                }
            }

            // get adapter features
            Console.WriteLine("Adapter features:");
            int featureCount= (int)_wgpu.AdapterEnumerateFeatures(_adapter, null);
            FeatureName* featureNames = stackalloc FeatureName[featureCount];
            _wgpu.AdapterEnumerateFeatures(_adapter, featureNames);
            for (int i = 0; i < featureCount; i++)
            {
                Console.WriteLine($"\t{featureNames[i]}");
            }

            // get adapter properties
            Console.WriteLine("Adapter properties:");
            AdapterProperties properties = new AdapterProperties();
            _wgpu.AdapterGetProperties(_adapter,ref properties);
            foreach (var fieldInfo in properties.GetType().GetFields())
            {
                if (fieldInfo.FieldType.IsPointer)
                {
                    IntPtr value = new IntPtr(Pointer.Unbox(fieldInfo.GetValue(properties)));
                    if(value!= IntPtr.Zero)
                        Console.WriteLine($"\t{fieldInfo.Name}: {Marshal.PtrToStringUTF8(value)}");
                }
                else
                {
                    Console.WriteLine($"\t{fieldInfo.Name}: {fieldInfo.GetValue(properties)}");
                }
               
            }
        }

        private static void InspectDevice()
        {
            // get device limits
            Console.WriteLine("Device limits:");
            SupportedLimits supportedLimits = new SupportedLimits();
            if (_wgpu.DeviceGetLimits(_device, ref supportedLimits))
            {
                foreach (var fieldInfo in supportedLimits.Limits.GetType().GetFields())
                {
                    Console.WriteLine($"\t{fieldInfo.Name}: {fieldInfo.GetValue(supportedLimits.Limits)}");
                }
            }

            // get device features
            Console.WriteLine("Device features:");
            int featureCount = (int)_wgpu.DeviceEnumerateFeatures(_device, null);
            FeatureName* featureNames = stackalloc FeatureName[featureCount];
            _wgpu.AdapterEnumerateFeatures(_adapter, featureNames);
            for (int i = 0; i < featureCount; i++)
            {
                Console.WriteLine($"\t{featureNames[i]}");
            }
        }

        private static void OnDeviceLostCallBack(DeviceLostReason reason, byte* message, void* _)
        {
            throw new Exception($"lost reason:{reason},{Marshal.PtrToStringUTF8((IntPtr)message)}");
        }
    }
}

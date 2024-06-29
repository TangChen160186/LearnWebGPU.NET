using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace _3_Adapter
{
    internal unsafe class Program
    {
        private static IWindow _window;

        private static WebGPU _wgpu;
        private static Instance* _instance;
        private static Adapter* _adapter;
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
            _wgpu.InstanceRequestAdapter(_instance, new RequestAdapterOptions(){BackendType = BackendType.OpenGL}, pfnRequestAdapterCallback, default);
            InspectAdapter();
            


     
        }

        private static void WindowOnUpdate(double obj)
        {
           
        }



        private static void WindowRender(double obj)
        {
       
        }

        private static void WindowClosing()
        {
            _wgpu.AdapterRelease(_adapter);
            _wgpu.InstanceRelease(_instance);

            // you can only call Dispose to release all unmanaged resources
            //_wgpu.Dispose();
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
    }
}

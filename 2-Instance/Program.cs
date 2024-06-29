using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace _2_Instance
{
    internal unsafe class Program
    {
        private static IWindow _window;


        private static WebGPU _wgpu;
        private static Instance* _instance;
        static void Main(string[] args)
        {
            _window = Window.Create(WindowOptions.Default with{
                API = GraphicsAPI.None,
                WindowBorder = WindowBorder.Fixed
            });

            _window.Initialize();
            _window.Center();
            _window.Load += WindowLoad;
            _window.Render += WindowRender;
            _window.Update += WindowOnUpdate;
            _window.Closing += WindowClosing;

            _window.Run();
        }
        private static void WindowLoad()
        {
            _wgpu = WebGPU.GetApi();
            _instance = _wgpu.CreateInstance(new InstanceDescriptor());
            if (_instance is null)
                throw new Exception("instance init failure");
            

        }

        private static void WindowOnUpdate(double obj)
        {
            Console.WriteLine("WindowOnUpdate");
        }



        private static void WindowRender(double obj)
        {
            Console.WriteLine("window_Render");
        }

        private static void WindowClosing()
        {
            _wgpu.InstanceRelease(_instance);
        }
    }
}

using Silk.NET.Windowing;

namespace _1_CreateWindow
{
    internal class Program
    {
        private static IWindow _window;
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

        private static void WindowOnUpdate(double obj)
        {
            Console.WriteLine("WindowOnUpdate");
        }

        private static void WindowClosing()
        {
            Console.WriteLine("Closing");
        }

        private static void WindowRender(double obj)
        {
            Console.WriteLine("window_Render");
        }

        private static void WindowLoad()
        {
            Console.WriteLine("window_Load");
        }
    }
}

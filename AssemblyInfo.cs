using System.Windows;
using System.Runtime.CompilerServices;

// An assembly is a compiled .dll or .exe. Attributes beginning with "assembly:"
// describe this whole compiled project rather than one class. Allow our tests to
// inspect internal helpers (such as layout) without making those helpers public.
[assembly: InternalsVisibleTo("BitExplorer.WpfTests")]

// WPF searches these locations for fallback styles when a resource was not found
// on the control or in App.xaml. We do not ship separate theme-specific dictionaries.
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]

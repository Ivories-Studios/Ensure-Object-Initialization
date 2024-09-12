# Ensure-Object-Initialization

Unity Objects can only be created with an Instantiate call. This unfortunately means that you cannot write a constructor for them.

A common workaround is to create an Initialize function that is called after the object is created. This analyzer will ensure that the Initialize function is called before the object is used.

## How to use

1. Add the dll found in the Releases section to the Assets folder of your Unity project
2. Click on the dll to see its import settings
3. Disable *Any Platform* and then disable all of the platforms under *Include Platforms*
4. Add **RoslynAnalyzer** as an asset tag (bottom right).

Add the `[RequiresInitialization("YourInitializeFunction")]`attribute to the class.

```csharp
[RequiresInitialization("Initialize")]
public class ExampleClass : MonoBehaviour
{
    public void Initialize()
    {
        // Initialization code
    }
}
```

```csharp
public class ExampleSpawner : MonoBehaviour
{
    public ExampleClass examplePrefab;

    private void Start()
    {
        ExampleClass example = Instantiate(examplePrefab); // this will now throw an error
    }
}
```

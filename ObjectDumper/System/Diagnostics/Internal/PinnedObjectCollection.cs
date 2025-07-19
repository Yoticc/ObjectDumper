using System.Reflection;
using System.Runtime.InteropServices;

class PinnedObjectCollection : IDisposable
{
    const int InitialCapacity = 1 << 8;

    List<nint> objects = new List<nint>(InitialCapacity);
    List<GCHandle> handles = new List<GCHandle>(InitialCapacity);

    public void AddObject(object @object)
    {
        var handle = HandleManager.AllocateHandle(@object);
        handles.Add(handle);


    }

    public int GetObjectIndex(nint @object)
    {
        var objects = this.objects;
        var objectsCount = objects.Count;
        for (var index = 0; index < objectsCount; index++)
            if (objects[index] == @object)
                return index;

        return -1;
    }

    public void Dispose()
    {
        foreach (var handle in handles)
            handle.Free();
    }

    delegate GCHandle InternalAllocDelegate(object value, GCHandleType type);

    static InternalAllocDelegate InternalAlloc = typeof(GCHandle).GetMethod("InternalAlloc", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<InternalAllocDelegate>();

    public static GCHandle
}
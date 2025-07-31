using System.Reflection;
using System.Runtime.InteropServices;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
unsafe class PinnedObjectCollection : IDisposable
{
    const int InitialCapacity = 1 << 8;

    List<nint> objects = new List<nint>(InitialCapacity);
    List<GCHandle> handles = new List<GCHandle>(InitialCapacity);

    public int Count => objects.Count;

    public nint this[int index] => objects[index];

    public int TryAddObject(object @object)
    {
        var pobject = *(nint*)&@object;
        var index = GetObjectIndex(pobject);

        if (index == -1)
        {
            index = Count;
            AddObject(@object);
        }

        return index;
    }

    public void AddObject(object @object)
    {
        var handle = InternalAlloc(@object, GCHandleType.Pinned);
        handles.Add(*(GCHandle*)&handle);

        var pobject = *(nint*)&@object;
        objects.Add(pobject);
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

    delegate nint InternalAllocDelegate(object value, GCHandleType type);
    static InternalAllocDelegate InternalAlloc = typeof(GCHandle).GetMethod("InternalAlloc", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<InternalAllocDelegate>();
}
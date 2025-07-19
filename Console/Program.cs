using System.Diagnostics;

var @object = new A();
LayoutVerifier.Verify(@object);
Console.ReadLine();

class A_
{
    public string StringField = "Some string here!";
    public byte[] ByteArrayField = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF];
}

class A : A_
{
    public int IntField1 = 0x7080;
    public ulong LongField = 0xE700660099661818UL;
    public int IntField2 = 0x0C;
    public B BField = new B();
}

class B
{
    public B() => SelfField = this;

    public B SelfField;
    public C StructField;
}

struct C
{
    public C() { }

    public int XValue = 10;
    public int YValue = 20;
}
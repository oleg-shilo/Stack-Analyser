# Stack-Analyser
A playground for dealing with call stack and assembly metadata 

The typical challenge of working with `StackTrace` is the difficulties of resolving local variables names. This is due to the fact the the variable names are not stored in the stack and but their indexes only. Thus index is the variable identity. 

This solution shows how to read the missing info (variable names) from teh metadata:   


```C#
class Program
{
    static void Main()
    {
        method2();
    }

    static void method1()
    {
        var testVar1 = "asdASDAS"; Inspect();
    }

    static void method2()
    {
        var testVar2 = "ccccccccccccc";
        var testVar3 = "ccccccccccccc"; Inspect();

        for (int i = 0; i < 2; i++)
        {
            var testVar4 = "ccccccccccccc";
            var testVar5 = "ccccccccccccc";
        }
        method1();
    }
}
```

![image](https://user-images.githubusercontent.com/16729806/199244048-fbb139cd-0305-4f21-8541-15b80bf94510.png)



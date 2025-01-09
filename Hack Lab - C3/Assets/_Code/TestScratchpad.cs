using Latios.Unika;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace C3
{
    public partial interface ITestInterface : IUnikaInterface
    {
        int prop { get; set; }

        int this[int index] { get; set; }
    }

    //public struct TestStruct
    //{
    //    public int get_prop() => 3;
    //}
    //
    //public static class TestRun
    //{
    //    public static void Run()
    //    {
    //        var p = new TestStruct().prop;
    //        UnityEngine.Debug.Log
    //    }
    //}
}


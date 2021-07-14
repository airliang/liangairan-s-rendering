using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BVHSort
{
    public delegate bool CompareFunc<T>(List<T> list, int a, int b);
    public delegate void SwapFunc<T>(List<T> list, int a, int b);
    private static int QSORT_STACK_SIZE = 32;
    private static int QSORT_MIN_SIZE = 16;

    static void InsertionSort<T>(int start, int size, List<T> list, CompareFunc<T> compareFunc, SwapFunc<T> swapFunc)
    {
        for (int i = 1; i < size; i++)
        {
            int j = start + i - 1;
            while (j >= start && compareFunc(list, j + 1, j))
            {
                swapFunc(list, j, j + 1);
                j--;
            }
        }
    }

    static void Swap<T>(ref T a, ref T b)
    {
        T tmp = a;
        a = b;
        b = tmp;
    }
    static int Median3<T>(int low, int high, List<T> list, CompareFunc<T> compareFunc)
    {
        //FW_ASSERT(compareFunc);
        //FW_ASSERT(low >= 0 && high >= 2);

        int l = low;
        int c = (low + high) >> 1;
        int h = high - 2;

        if (compareFunc(list, h, l)) 
            Swap(ref l, ref h);
        if (compareFunc(list, c, l)) 
            c = l;
        return (compareFunc(list, h, c)) ? h : c;
    }
    static int Partition<T>(int low, int high, List<T> list, CompareFunc<T> compareFunc, SwapFunc<T> swapFunc)
    {
        // Select pivot using median-3, and hide it in the highest entry.

        swapFunc(list, Median3(low, high, list, compareFunc), high - 1);

        // Partition data.

        int i = low - 1;
        int j = high - 1;
        for (; ; )
        {
            do
                i++;
            while (compareFunc(list, i, high - 1));
            do
                j--;
            while (compareFunc(list, high - 1, j));

            //FW_ASSERT(i >= low && j >= low && i < high && j < high);
            if (i >= j)
                break;

            swapFunc(list, i, j);
        }

        // Restore pivot.

        swapFunc(list, i, high - 1);
        return i;
    }
    public static void Sort<T>(int start, int end, List<T> list, CompareFunc<T> compareFunc, SwapFunc<T> swapFunc)
    {
        if (end - start < 2)
            return;

        int[] stack = new int[QSORT_STACK_SIZE];
        int sp = 0;
        stack[sp++] = end;
        int high = end;
        int low = start;

        while (sp != 0)
        {
            high = stack[--sp];
            //FW_ASSERT(low <= high);

            // Small enough or stack full => use insertion sort.

            if (high - low < QSORT_MIN_SIZE || sp + 2 > QSORT_STACK_SIZE)
            {
                InsertionSort(low, high - low, list, compareFunc, swapFunc);
                low = high + 1;
                continue;
            }

            // Partition and sort sub-partitions.

            int i = Partition(low, high, list, compareFunc, swapFunc);
            //FW_ASSERT(sp + 2 <= QSORT_STACK_SIZE);
            if (high - i > 2)
                stack[sp++] = high;
            if (i - low > 1)
                stack[sp++] = i;
            else
                low = i + 1;
        }
    }
}

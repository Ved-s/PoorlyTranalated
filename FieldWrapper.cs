using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace PoorlyTranslated
{
    public class FieldWrapper<T>
    {
        delegate ref T RefGetterDelegate(object? instance);

        readonly Action<object?, T> Setter;
        readonly Func<object?, T> Getter;
        readonly RefGetterDelegate RefGetter;

        object? Instance;

        public T Value
        {
            get => Getter(Instance);
            set => Setter(Instance, value);
        }

        public ref T ValueRef => ref RefGetter(Instance);

        public FieldWrapper(object? instance, Type type, string fieldName) : this(instance, type.GetField(fieldName, (BindingFlags)(-1)))
        {
        }

        public FieldWrapper(object? instance, FieldInfo field)
        {
            if (field.FieldType != typeof(T))
                throw new ArgumentException("Field type must match wrapper generic", nameof(field));

            Instance = instance;

            DynamicMethodDefinition dm = new($"get_FieldWrapper_{typeof(T).Name}_{field.Name}", typeof(T), new[] { typeof(object) });
            var il = dm.GetILGenerator();

            if (field.IsStatic)
            {
                il.Emit(OpCodes.Ldsfld, field);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
            }
            il.Emit(OpCodes.Ret);
            Getter = dm.Generate().CreateDelegate<Func<object?, T>>();


            dm = new($"set_FieldWrapper_{typeof(T).Name}_{field.Name}", null, new[] { typeof(object), typeof(T) });
            il = dm.GetILGenerator();

            if (field.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stsfld, field);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stfld, field);
            }
            il.Emit(OpCodes.Ret);
            Setter = dm.Generate().CreateDelegate<Action<object?, T>>();


            dm = new($"ref_FieldWrapper_{typeof(T).Name}_{field.Name}", typeof(T).MakeByRefType(), new[] { typeof(object) });
            il = dm.GetILGenerator();

            if (field.IsStatic)
            {
                il.Emit(OpCodes.Ldsflda, field);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, field);
            }
            il.Emit(OpCodes.Ret);
            RefGetter = dm.Generate().CreateDelegate<RefGetterDelegate>();
        }

        public T Get(object? instance) => Getter(instance);
        public void Set(object? instance, T value) => Setter(instance, value);
        public ref T GetRef(object? instance) => ref RefGetter(instance);
    }
}

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

public abstract class AbstractUdonProgramSource : ScriptableObject
{
    [PublicAPI]
    public abstract IUdonProgram GetProgram();

    [PublicAPI]
    public abstract void RunProgramSourceEditor(Dictionary<string, (object value, Type declaredType)> publicVariables, ref bool dirty);

    [PublicAPI]
    public abstract void RefreshProgram();
}

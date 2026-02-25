using System;
using Assets.Scripts.NPCs.Jobs.Drivers;

namespace Assets.Scripts.NPCs.Jobs
{
    /// <summary>
    /// Defines a type of job. Each JobDef knows which JobDriver executes it.
    /// Inspired by RimWorld's JobDef: adding a new behavior means creating a new
    /// JobDef + JobDriver pair — no modifications to existing code.
    /// </summary>
    public class JobDef
    {
        public string Name { get; }
        public Type DriverType { get; }

        public JobDef(string name, Type driverType)
        {
            Name = name;
            DriverType = driverType;
        }

        public JobDriver CreateDriver()
        {
            return (JobDriver)Activator.CreateInstance(DriverType);
        }

        public override string ToString() => Name;

        // Built-in job definitions
        public static readonly JobDef MoveTo = new("MoveTo", typeof(JobDriver_MoveTo));
        public static readonly JobDef Interact = new("Interact", typeof(JobDriver_Interact));
        public static readonly JobDef Farm = new("Farm", typeof(JobDriver_Farm));
        public static readonly JobDef Mine = new("Mine", typeof(JobDriver_Mine));
    }
}

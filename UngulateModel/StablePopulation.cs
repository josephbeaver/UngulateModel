using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using static BeaverUtils.ModelUtils;

namespace UngulateModel
{
    [Serializable]
    class StablePopulation
    {
        public string PopName { get; }
        public ParameterSet PSet { get; }
        public int[,] Cohort { get { return (int[,])cohort.Clone(); } }
        public decimal CarryingCapacity { get; private set; }
        public bool Stabilized { get; private set; }
        public int Size { get { return targetSize; } }
        public int TimeSteps { get; private set; }


        private int[,] cohort;
        private int targetSize;
        private bool onTarget = false;

        [NonSerialized] private Population pop;
        [NonSerialized] private int consecutiveTargets = 0;
        [NonSerialized] private int year = 0;


        public StablePopulation(ParameterSet pSet, int size, int timeSteps)
        {
            PopName = pSet.ModelName + size;
            PSet = pSet;
            TimeSteps = timeSteps;
            targetSize = size;

            pop = new Population(pSet, size, timeSteps);
            Stabilized = Stabilize(pop);

            if (Stabilized)
            {
                CopyData(pop);
                SaveToFile();
            }
        }

        private void CopyData(Population pop)
        {
            CarryingCapacity = pop.CarryingCapacity;
            cohort = (int[,])pop.Cohort.Clone();
        }

        private void SaveToFile()
        {
            if (!Directory.Exists($"TS_{TimeSteps}"))
            {
                Directory.CreateDirectory($"TS_{TimeSteps}");
            }

            using (Stream s = new FileStream($"TS_{TimeSteps}\\{PopName}_spop.bin", FileMode.Create, FileAccess.Write))
            {
                SrzFormatter.Serialize(s, this);
            }
        }

        public override string ToString()
        {
            string nl = Environment.NewLine;

            int totalPop = 0;
            string cohortData = "";
            for (int y = 0; y <= PSet.AgeMaximum; y++)
            {
                cohortData += y + ":\t" + cohort[0, y] + "\t" + cohort[1, y] + nl;
                totalPop += cohort[0, y] + cohort[1, y];
            }

            string s = PopName + " " + nl;
            s += "Pre-reproduction population size: " + totalPop + nl;
            s += "Carrying capacity: " + CarryingCapacity + nl;
            s += "Age\tFemale\tMale" + nl;
            return s + cohortData;
        }


        private bool Stabilize(Population pop)
        {
            while (!onTarget && year < 30000)
            {
                SimulationEnding se = pop.SimulateYear(null, null, null, null, null, YearEndChecksAndCarryCapAdjustment);
                year++;
            }

            Console.WriteLine();
            return onTarget;
        }


        private SimulationEnding YearEndChecksAndCarryCapAdjustment()
        {
            decimal targetRatio = (decimal)targetSize / pop.AveragePop(consecutiveTargets + 5);
            if (Math.Abs(targetRatio - 1m) < .005m)
            {
                consecutiveTargets++;
            }
            else
            {
                consecutiveTargets = 0;
                if (year % 5 == 0)
                {
                    pop.AdjustCarryingCapacity((targetRatio * 2m + 1m) / 3m);
                }
            }

    //        DisplayPopSize();

            if (consecutiveTargets == 5 * PSet.AgeMaximum)
            {
                onTarget = true;
            }

            return SimulationEnding.NotYet;
        }

        private void DisplayPopSize()
        {
            Console.WriteLine();

            int numFiftieths = (int)Math.Round(pop.LastYearAveragePop() * 50 / targetSize);
            for (int i = 0; i < numFiftieths; i++)
            {
                Console.Write(i != 50 ? "*" : "|");
            }
            for (int i = numFiftieths; i < 65; i++)
            {
                Console.Write(i != 50 ? " " : "|");
            }
            Console.Write("\t" + (int)Math.Round(pop.LastYearAveragePop() - targetSize));
        }

        public static StablePopulation LoadFromFile(string pSetName, string size, string timesteps)
        {
            string path = $"TS_{timesteps}\\{pSetName}{size}_spop.bin";
            if (!File.Exists(path))
            {
                return null;
            }

            StablePopulation sp;
            using (Stream s = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                sp = (StablePopulation)SrzFormatter.Deserialize(s);
            }

            return sp;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using static BeaverUtils.ModelUtils;

namespace UngulateModel
{
    class SinglePopulationSimulation
    {
        protected static readonly int FEMALE = 0;
        protected static readonly int MALE = 1;

        protected Population pop;
        protected int unhuntedPopSize;
        protected decimal currentHarvestRate;
        protected decimal annualHarvestRateIncreaseLinear;
        protected decimal annualHarvestRateIncreaseExponential;
        protected int minHarvestAgeInTimeSteps;
        protected int[] harvestByStep;
        protected List<List<int[][]>> harvestSets;
        protected List<int[,]> populationSets;
        protected List<string> stableFinal;
        protected Random rand;
        protected string outputFile;

        protected bool ternaryAgeStructure = false;
        protected int juvCutoff;
        protected int primeCutoff;

        public SinglePopulationSimulation(StablePopulation sPop, decimal initialHarvestRate, decimal annualHarvestRateIncreaseLinear, decimal annualHarvestRateIncreaseExponential, decimal minimumHarvestAge, string outFilePath, int? randSeed = null)
        {
            rand = randSeed.HasValue ? new Random(randSeed.Value) : new Random();
            outputFile = outFilePath;

            pop = new Population(sPop, rand);
            unhuntedPopSize = sPop.Size;
            currentHarvestRate = initialHarvestRate;
            this.annualHarvestRateIncreaseLinear = annualHarvestRateIncreaseLinear;
            this.annualHarvestRateIncreaseExponential = annualHarvestRateIncreaseExponential;
            minHarvestAgeInTimeSteps = (int)Math.Ceiling(pop.TimeSteps * minimumHarvestAge);
            harvestSets = new List<List<int[][]>>();
            populationSets = new List<int[,]>();

            stableFinal = new List<string>();
            stableFinal.Add("0");   // pre-simulation year
            AddArrayToList(stableFinal, sPop.Cohort);
        }

        public SimulationEnding Run()
        {
            SimulationEnding se = SimulationEnding.NotYet;
            while (se == SimulationEnding.NotYet)
            {
                se = pop.SimulateYear(PrepRecording, ScheduleHuntingMortality, null, HuntPopulation, RecordKeeping, UpdateHarvestRate);
            }

            SaveDataToOutfile();

            return se;

        }

        public SimulationEnding PrepRecording()
        {
            harvestSets.Add(new List<int[][]>());
            return SimulationEnding.NotYet;
        }

        public void SetTernaryAgeGroups(decimal juvenileMaximum, decimal primeMaximum)
        {
            ternaryAgeStructure = true;
            juvCutoff = (int)Math.Floor(juvenileMaximum * pop.TimeSteps);
            primeCutoff = (int)Math.Floor(primeMaximum * pop.TimeSteps);
        }

        protected void SaveDataToOutfile()
        {
            using (StreamWriter stw = new StreamWriter(outputFile))
            {
                List<string> headers = new List<string>();
                headers.Add("Year");
                if (ternaryAgeStructure)
                {
                    AddSequencesToList(headers, "Pop_F Pop_M".Split(' '), 0, pop.PSet.AgeMaximum);
                    headers.AddRange("Hrv_FJ Hrv_FP Hrv_FO Hrv_MJ Hrv_MP Hrv_MO".Split(' '));
                }
                else
                {
                    AddSequencesToList(headers, "Pop_F Pop_M Hrv_F Hrv_M".Split(' '), 0, pop.PSet.AgeMaximum);
                }
                WriteToTabDelim(headers, stw);
                WriteToTabDelim(stableFinal, stw);

                for (int y = 0; y < populationSets.Count; y++)
                {
                    List<string> row = new List<string>();
                    row.Add((y + 1).ToString());    // output years should start at one
                    AddArrayToList(row, populationSets[y]);
                    AddArrayToList(row, ternaryAgeStructure ? CountByGroup(harvestSets[y]) : CountByCohort(harvestSets[y]));
                    WriteToTabDelim(row, stw);
                }

            }
        }

        private int[,] CountByGroup(List<int[][]> harvestSet)
        {
            int[,] groupCounts = new int[2, 3];

            foreach (int[][] subset in harvestSet)
            {
                for (int s = FEMALE; s <= MALE; s++)
                {
                    foreach (int tsAge in subset[s])
                    {
                        groupCounts[s, (tsAge <= juvCutoff) ? 0 : (tsAge <= primeCutoff) ? 1 : 2]++;
                    }
                }
            }
            return groupCounts;
        }

        private int[,] CountByCohort(List<int[][]> harvestSet)
        {
            int[,] groupCounts = new int[2, pop.PSet.AgeMaximum + 1];
            foreach (int[][] subset in harvestSet)
            {
                for (int s = FEMALE; s <= MALE; s++)
                {
                    foreach (int tsAge in subset[s])
                    {
                        groupCounts[s, tsAge / pop.TimeSteps]++;
                    }
                }
            }
            return groupCounts;
        }

        protected SimulationEnding RecordKeeping()
        {
            populationSets.Add((int[,])pop.Cohort.Clone());
            return SimulationEnding.NotYet;
        }


        private void DisplayPopSize(int simYear)
        {
            Console.Write("\r");
            string year = simYear.ToString();
            Console.Write(year);
            for (int i = year.Length; i < 8; i++)
            {
                Console.Write(" ");
            }
            int numFiftieths = (int)Math.Round(pop.LastYearAveragePop() * 50 / unhuntedPopSize);
            for (int i = 0; i < numFiftieths; i++)
            {
                Console.Write(i != 50 ? "*" : "|");
            }
            for (int i = numFiftieths; i < 65; i++)
            {
                Console.Write(i != 50 ? " " : "|");
            }
        }

        protected SimulationEnding HuntPopulation(int step)
        {
            int[][] harvest = pop.HarvestRandom(harvestByStep[step % pop.TimeSteps], minHarvestAgeInTimeSteps, step);


            if (harvest == null)
            {
                return SimulationEnding.PopulationCrashed;
            }

            harvestSets[harvestSets.Count - 1].Add(harvest);

            return SimulationEnding.NotYet;
        }

        protected SimulationEnding ScheduleHuntingMortality()
        {
            harvestByStep = new int[pop.TimeSteps];

            // determine population size to use as basis for calculating hunting mortality
            decimal effectivePopSize = pop.LastYearAveragePop();
            if (effectivePopSize < 0m) effectivePopSize = (decimal)unhuntedPopSize; //override where population doesn't have a history to use

            // determine actual number of kills to schedule
            int numKills = ProbabilisticRound(currentHarvestRate * effectivePopSize);

            //distribute kills randomly through the year
            for (int i = 0; i < numKills; i++)
            {
                harvestByStep[rand.Next(pop.TimeSteps)]++;
            }

            return SimulationEnding.NotYet;
        }


        protected SimulationEnding UpdateHarvestRate()
        {
            if (annualHarvestRateIncreaseLinear > 0m)
            {
                currentHarvestRate += annualHarvestRateIncreaseLinear;
            }
            else
            {
                currentHarvestRate *= (1m + annualHarvestRateIncreaseExponential);
            }

            return SimulationEnding.NotYet;
        }
    }
}

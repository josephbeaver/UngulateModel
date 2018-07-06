using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using static BeaverUtils.ModelUtils;

namespace UngulateModel
{
    class SinglePopulationSexBiasedSimulation : SinglePopulationSimulation
    {
        private decimal harvestSexRatio;
        new private int[,] harvestByStep;


        public SinglePopulationSexBiasedSimulation(StablePopulation sPop, decimal initialHarvestRate, decimal annualHarvestRateIncreaseLinear, decimal annualHarvestRateIncreaseExponential, decimal harvestFemaleFraction, decimal minimumHarvestAge, string outFilePath, int? randSeed = null) : base(sPop, initialHarvestRate, annualHarvestRateIncreaseLinear, annualHarvestRateIncreaseExponential, minimumHarvestAge, outFilePath, randSeed)
        {
            harvestSexRatio = harvestFemaleFraction;
        }

        new public SimulationEnding Run()
        {
            SimulationEnding se = SimulationEnding.NotYet;
            while (se == SimulationEnding.NotYet)
            {
                se = pop.SimulateYear(PrepRecording, ScheduleHuntingMortality, null, HuntPopulation, RecordKeeping, UpdateHarvestRate);
            }

            SaveDataToOutfile();

            return se;

        }

        new private SimulationEnding HuntPopulation(int step)
        {
            int[][] harvest = new int[2][];
            harvest[FEMALE] = pop.HarvestRandomBySex(harvestByStep[FEMALE, step % pop.TimeSteps], FEMALE, minHarvestAgeInTimeSteps, step);
            harvest[MALE] = pop.HarvestRandomBySex(harvestByStep[MALE, step % pop.TimeSteps], MALE, minHarvestAgeInTimeSteps, step);

            if (harvest[FEMALE] == null || harvest[MALE] == null)
            {
                return SimulationEnding.SexBiasUnsustainable;
            }

            harvestSets[harvestSets.Count - 1].Add(harvest);

            return SimulationEnding.NotYet;
        }

        new private SimulationEnding ScheduleHuntingMortality()
        {
            harvestByStep = new int[2, pop.TimeSteps];

            // determine population size to use as basis for calculating hunting mortality
            decimal effectivePopSize = pop.LastYearAveragePop();
            if (effectivePopSize < 0m) effectivePopSize = (decimal)unhuntedPopSize; //override where population doesn't have a history to use

            // determine actual number of kills to schedule
            int numKills = ProbabilisticRound(currentHarvestRate * effectivePopSize);
            int femaleKills = ProbabilisticRound(harvestSexRatio * numKills);
            int maleKills = numKills - femaleKills;

            //distribute kills randomly through the year
            for (int i = 0; i < femaleKills; i++)
            {
                harvestByStep[FEMALE, rand.Next(pop.TimeSteps)]++;
            }
            for (int i = 0; i < maleKills; i++)
            {
                harvestByStep[MALE, rand.Next(pop.TimeSteps)]++;
            }

            return SimulationEnding.NotYet;
        }
    }
}

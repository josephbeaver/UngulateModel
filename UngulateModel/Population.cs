using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BeaverUtils.ModelUtils;

namespace UngulateModel
{
    class Population
    {
        private static readonly int FEMALE = 0;
        private static readonly int MALE = 1;

        public ParameterSet PSet { get; private set; }
        public int TimeSteps { get; private set; }
        public int[,] Cohort { get; private set; }
        public decimal CarryingCapacity { get; private set; }

        private Random rand;
        private decimal[] byStepJuvSurvival = { 0m, 0m };
        private int[] stepPopulations;
        private List<decimal> popHistory = new List<decimal>(1000);

        private decimal[] byStepAdultSurvival = { 0m, 0m };
        
        public Population(ParameterSet pSet, int targetSize, int timesteps)
        {
            PSet = pSet;
            TimeSteps = timesteps;
            Cohort = new int[2, pSet.AgeMaximum + 1];
            stepPopulations = new int[timesteps];
            CarryingCapacity = targetSize * 5; // heuristic initial estimate only

            double intercept = targetSize / pSet.AgeMaximum;
            double slope = -intercept / pSet.AgeMaximum;

            for (int s = FEMALE; s <= MALE; s++)
            {
                for (int y = 1; y <= pSet.AgeMaximum; y++) // skipping age 0 here as it will be filled by reproduction immediately
                {
                    Cohort[s, y] = (int)Math.Round(y * slope + intercept);
                }
            }
            byStepAdultSurvival[FEMALE] = (decimal)Math.Pow((double)(1m - pSet.FemaleAdultNatMortRate), 1.0 / TimeSteps);
            byStepAdultSurvival[MALE] = (decimal)Math.Pow((double)(1m - pSet.MaleAdultNatMortRate), 1.0 / TimeSteps);

            rand = new Random();
        }

        public Population(StablePopulation stablePop, Random rand)
        {
            PSet = stablePop.PSet;
            CarryingCapacity = stablePop.CarryingCapacity;
            Cohort = stablePop.Cohort;
            TimeSteps = stablePop.TimeSteps;
            stepPopulations = new int[TimeSteps];
            this.rand = rand;
            byStepAdultSurvival[FEMALE] = (decimal)Math.Pow((double)(1m - PSet.FemaleAdultNatMortRate), 1.0 / TimeSteps);
            byStepAdultSurvival[MALE] = (decimal)Math.Pow((double)(1m - PSet.MaleAdultNatMortRate), 1.0 / TimeSteps);
        }

        public SimulationEnding SimulateYear(Func<SimulationEnding> beforeAll, Func<SimulationEnding> postRepro, Func<int, SimulationEnding> stepPreNatMort, Func<int, SimulationEnding> stepPostNatMort, Func<SimulationEnding> postAllSteps, Func<SimulationEnding> afterAll)
        {
            SimulationEnding se = SimulationEnding.NotYet;

            if (beforeAll != null)
            {
                se = beforeAll();
                if (se != SimulationEnding.NotYet) return se;
            }

            Reproduction();

            if (postRepro != null)
            {
                se = postRepro();
                if (se != SimulationEnding.NotYet) return se;
            }

            for (int step = 0; step < TimeSteps; step++)
            {
                if (stepPreNatMort != null)
                {
                    se = stepPreNatMort(step);
                    if (se != SimulationEnding.NotYet) return se;
                }

                NaturalMortality(step);

                if (stepPostNatMort != null)
                {
                    se = stepPostNatMort(step);
                    if (se != SimulationEnding.NotYet) return se;
                }
            }

            if (postAllSteps != null)
            {
                se = postAllSteps();
                if (se != SimulationEnding.NotYet) return se;
            }

            AgeIncrementation();

            if (afterAll != null)
            {
                se = afterAll();
                if (se != SimulationEnding.NotYet) return se;
            }

            return SimulationEnding.NotYet;
        }

        public void AdjustCarryingCapacity(decimal adjustmentFactor)
        {
            if (adjustmentFactor > 0)
            {
                CarryingCapacity *= adjustmentFactor;
            }
            else
            {
                throw new ArgumentException("Carrying capacity adjustment factor must be positive and non-zero. Was: " + adjustmentFactor);
            }
        }

        public SimulationEnding NaturalMortality(int timeStep)
        {
            // juvenile mortality's relationship to population and carrying capacity is determined at the beginning of the model year
            if (timeStep % TimeSteps == 0)
            {
                decimal kFactor = TotalPopulation() < CarryingCapacity ? TotalPopulation() / CarryingCapacity : 1m; // cannot exceed 1
                byStepJuvSurvival[FEMALE] = (decimal)Math.Pow((double)(1m - (PSet.FemaleBaseJuvMortRate + kFactor * (1m - PSet.FemaleBaseJuvMortRate))), 1.0 / TimeSteps);
                byStepJuvSurvival[MALE] = (decimal)Math.Pow((double)(1m - (PSet.MaleBaseJuvMortRate + kFactor * (1m - PSet.MaleBaseJuvMortRate))), 1.0 / TimeSteps);
            } 

            // determine cohort that adult mortality applies to
            int adultMortAgeInTimeSteps = (int)Math.Ceiling(PSet.AgeOnsetAdultMortality * TimeSteps);
            int firstAdultMortCohort = adultMortAgeInTimeSteps / TimeSteps - (adultMortAgeInTimeSteps % TimeSteps <= timeStep % TimeSteps ? 0 : 1);

            for (int y = 0; y < firstAdultMortCohort; y++)
            {
                for (int s = FEMALE; s <= MALE; s++)
                {
                    Cohort[s, y] = ProbabilisticRound(byStepJuvSurvival[s] * Cohort[s, y]);
                }
            }

            for (int y = firstAdultMortCohort; y <= PSet.AgeMaximum; y++)
            {
                for (int s = FEMALE; s <= MALE; s++) {
                    Cohort[s, y] = ProbabilisticRound(byStepAdultSurvival[s] * Cohort[s, y]);
                }
            }

            int currentPop = TotalPopulation();
            stepPopulations[timeStep % TimeSteps] = currentPop;
            if (currentPop == 0) return SimulationEnding.PopulationCrashed;
            return SimulationEnding.NotYet;
        }

        public void Reproduction()
        {
            int reproAgeFemales = 0;
            for (int y = PSet.AgeOfFirstRepro; y <= PSet.AgeMaximum; y++)
            {
                reproAgeFemales += Cohort[FEMALE, y];
            }

            int newJuveniles = ProbabilisticRound(reproAgeFemales * PSet.PregnancyRate * PSet.MeanOffspring);

            // evenly distribute among males and females
            Cohort[FEMALE, 0] = Cohort[MALE, 0] = newJuveniles / 2;

            // probablilistically determine whether to make the odd individual (if any) male or female
            if (newJuveniles % 2 != 0)
            {
                Cohort[rand.Next(2), 0]++;
            }
        }

        public int[][] HarvestRandom(int numberToKill, int minAgeTimeSteps, int timeStep)
        {
            if (timeStep >= TimeSteps) throw new Exception("time steps are fucked up");

            // determine minimum cohort age for hunting--taking into account whether the simulation is far enough into the current year
            int minCohortYear = minAgeTimeSteps / TimeSteps + (timeStep > minAgeTimeSteps % TimeSteps ? 0 : 1);

            int huntablePop = 0;
            for (int s = FEMALE; s <= MALE; s++)
            {
                for (int y = minCohortYear; y <= PSet.AgeMaximum; y++)
                {
                    huntablePop += Cohort[s, y];
                }
            }

            // make sure the harvest by specific sex is still viable
            if (numberToKill > huntablePop)
            {
                return null;
            }

            List<int>[] agesKilled = new List<int>[2];
            for (int s = FEMALE; s <= MALE; s++)
            {
                agesKilled[s] = new List<int>();
            }

            for (int i = 0; i < numberToKill; i++)
            {
                // find age of individual chosen, with chance of age proportionate to cohort size
                int individualToKill = rand.Next(huntablePop);
                int y = minCohortYear;
                int s = FEMALE;
                int accumulation = Cohort[s, y];
                while (accumulation < individualToKill)
                {
                    if (y < PSet.AgeMaximum)
                    {
                        y++;
                    }
                    else  // shift to male cohorts
                    {
                        s = MALE;
                        y = minCohortYear;
                    }
                    accumulation += Cohort[s, y];
                }

                // decrement cohort and huntable pop so that next iteration uses correct values
                Cohort[s, y] -= 1;
                huntablePop -= 1;

                // set age in return array
                agesKilled[s].Add(y * TimeSteps + timeStep);
            }

            int[][] ages = new int[2][];
            ages[FEMALE] = agesKilled[FEMALE].ToArray();
            ages[MALE] = agesKilled[MALE].ToArray();

            return ages;

        }

        // return value is ages in timesteps; return of null indicates insufficient individuals of the specified sex
        public int[] HarvestRandomBySex(int numberToKill, int sex, int minAgeTimeSteps, int timeStep)
        {
            int timeStepOfYear = timeStep % TimeSteps;

            // determine minimum cohort age for hunting--taking into account whether the simulation is far enough into the current year
            int minCohortYear = minAgeTimeSteps / TimeSteps + (timeStepOfYear > minAgeTimeSteps % TimeSteps ? 0 : 1);

            int huntablePop = 0;
            for (int y = minCohortYear; y <= PSet.AgeMaximum; y++)
            {
                huntablePop += Cohort[sex, y];
            }

            // make sure the harvest by specific sex is still viable
            if (numberToKill > huntablePop)
            {
                return null;
            }

            int[] agesKilled = new int[numberToKill];
            for (int i = 0; i < numberToKill; i++)
            {
                // find age of individual chosen, with chance of age proportionate to cohort size
                int individualToKill = rand.Next(huntablePop);
                int y = minCohortYear;
                int accumulation = Cohort[sex, y];
                while (accumulation < individualToKill)
                {
                    y++;
                    accumulation += Cohort[sex, y];
                }
                
                // decrement cohort and huntable pop so that next iteration uses correct values
                Cohort[sex, y] -= 1;
                huntablePop -= 1;

                // set age in return array
                agesKilled[i] = y * TimeSteps + timeStepOfYear;
            }

            return agesKilled;
                
        }
            
        public void AgeIncrementation()
        {
            for (int sex = FEMALE; sex <= MALE; sex++)
            {
                for (int i = PSet.AgeMaximum; i > 0; i--)
                {
                    Cohort[sex, i] = Cohort[sex, i - 1];
                }
            }
            Cohort[FEMALE, 0] = 0;  // these are not strictly necessary, but are included to ensure that end of year values are correct
            Cohort[MALE, 0] = 0;
            popHistory.Add((decimal)stepPopulations.Sum() / TimeSteps);
        }

        public int TotalPopulation()
        {
            int popCount = 0;
            for (int sex = FEMALE; sex <= MALE; sex++)
            {
                for (int i = 0; i <= PSet.AgeMaximum; i++)
                {
                    popCount += Cohort[sex, i];
                }
            }
            return popCount;
        }

        public decimal AveragePop(int startYear, int endYear)
        {
            if (startYear < 0 || endYear < 0 || startYear >= popHistory.Count || endYear >= popHistory.Count)
            {
                throw new ArgumentException("Start or end year is outside the usable range");
            }

            decimal cumPops = 0m;
            for (int y = startYear; y <= endYear; y++)
            {
                cumPops += popHistory[y];
            }
            return (cumPops / (endYear - startYear + 1));
        }

        public decimal AveragePop(int nMostRecentYears)
        {
            if (nMostRecentYears > popHistory.Count)
            {
                nMostRecentYears = popHistory.Count;
            }
            return AveragePop(popHistory.Count - nMostRecentYears, popHistory.Count - 1);
        }

        public decimal LastYearAveragePop()
        {
            return popHistory.Count >= 1 ? popHistory[popHistory.Count - 1] : -1m;        
        }


    }
}

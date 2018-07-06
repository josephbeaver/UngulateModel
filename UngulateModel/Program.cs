using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BeaverUtils.ConsoleUtils;



namespace UngulateModel
{
    public enum SimulationEnding { NotYet, Scheduled, PopulationCrashed, SexBiasUnsustainable } 

    class Program
    {
        static void Main(string[] args)
        {
            /* Parse and Route Command */
            if (args.Length < 1)
            {
                ShowUsage();
            }
            else
            {
                switch (args[0])
                {
                    case "param_set":
                        ParamSet(args);
                        break;
                    case "stable_pop":
                        StablePop(args);
                        break;
                    case "max_harvest":
                        MaxHarvest(args);
                        break;
                    case "dyn_harvest":
                        DynHarvest(args);
                        break;
                    case "-h":
                    case "-help":
                    default:
                        ShowUsage();
                        break;
                }
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine(@"Usage:	UngulateModel param_set [ [ -e | -n | -t ] <paramsetname> ]

| stable_pop | max_harvest | dyn_harvest ] {[-b] <file> [<outfile>] | [-t <specfile>]}

Options:
    param_set       create or edit parameter sets; add -h for details
	stable_pop		produce a stable population to be used in simulation runs
	max_harvest		determine the resilience of a population to hunting pressure
	dyn_harvest		simulate the historical trajectory of a population as hunting pressure increases
	-b			    indicates that <file> contains multiple sets of specifications for simulation runs
	<file>			the name of the file with the relevant model or simulation parameters
	<outfile>		the file to write results to (note: stable_pop ignores this value if it is provided)

    -t <specfile>   write a text file to act as a template for the specified command


<file> details
	In all cases, the order of rows in a specification file is irrelevant; line labels (information before the 
	colon), however, must be exact or UngulateModel will not execute the instructions. If the file is preceded by
	the -b flag, the individual command data sets should be separated by a line starting with an asterisk

stable_pop	This command produces a file representing a stable population averaging a specified size, based on a 
		particular prey species model and using a specified number of time steps per year. Note that the 
		parameter data are hard-written into the stable-population file, so later changes to a file specifying
		a species model's parameters will not affect previously created stable populations.

   Rows:  ParameterSet: <name_of_model>		# The model name must have no spaces and match the name (except for the
						# extension) of a text file holding properly formatted parameters.
	  Size: <integet_value>			# Indicates the size that the population should be stable at (on average)
						# prior to the introduction of human hunting.
	  TimeSteps: <integer_value>		# The number of time steps per year for all simulations using this population.


max_harvest	This command runs a series of simulations to determine the maximum harvest rate that a particular population
		can sustain. By default, hunting is unselective except for a minimum age that is targeted. A sex bias can be
		optionally specified. The output shows the proportion of simulations where the population has not crashed at 
		each level of harvest, and calculates the mean maximum sustainable harvest rate from these data.

   Rows:  StablePop: <population>		# A previously created stable population; the name will be of the format
						# model_size_timesteps.
	  MinimumAgeHunted: <decimal_value>	# The age, in years, at which animals in the population become subject to
						# hunting by humans.
	  YearsPerRun: <integer_value>		# The number of years to run each simulation (subject to population survival).
	  Iterations: <integer_value>		# The number of simulations to run at each tested harvest rate.
	  FemaleBias: <decimal_value>		# Optional. If specified, this is the proportion of all kills that will be of
						# females. Note that a simulation run may end early if this bias cannot be
						# maintained due to population structure.

dyn_harvest	This command runs one or more simulations of a population subjected to a monotonically varying harvest rate.
		Yearly population averages and harvested animal information are recorded for each sex/age group. By default, 
		hunting is unselective except for a minimum age that is targeted. A sex bias can be optionally specified.
		Note that either LinearIncreaseRate or ExpoIncreaseRate must be specified.

   Rows:  StablePop: <population>		# A previously created stable population; the name will be of the format
						# model_size_timesteps.
      OutputFileName: <file_name>   # The file to write tab-delimited simulation data to. If the file already exists, it will be overwritten.
	  MinimumAgeHunted: <decimal_value>	# The age, in years, at which animals in the population become subject to
						# hunting by humans.
	  InitialHarvestRate: <decimal_value>	# The proportion of the _current_ population that is to be killed each year.
	  LinearIncreaseRate: <decimal_value>   # May be used in place of ExpoIncreaseRate. The value to be added each year to
						# the proportion of the current population that is to be killed per year.
	  ExpoIncreaseRate: <decimal_value>	    # May be used in place of LinearIncreaseRate. The annual exponential rate of 
						# increase in the proportion of the current population that is to be killed.
      FemaleProportion: <decimal_value>     # Optional. If specified, this fraction of kills from simulated hunting will be of females.
      JuvenileMaximumAge: <decimal_value>   # Optional. If this and the Prime equivalent are specified, output will show population and harvest in three age groups. Otherwise, both will be shown by age cohort.
      PrimeMaximumAge: <decimal_value>      # Optional. See JuvenileMaximumAge.
	  Iterations: <integer_value>		# Optional. The number of simulations to run (default is 1). Data are not averaged, but rather included
						# as additional columns in the output file.
      RNGSeed: <integer_value>          # Optional. A random number seed that will make the simulation repeatable.
");
        }

 

        private static void DynHarvest(string[] args)
        {
            if (args.Length < 2 || args[1] == "-h" || args[1] == "--help")
            {
                DisplayDynHarvestUsage();
                return;
            }

            if (args[1] == "-t")
            {
                WriteDynHarvestTemplate(args[2]);
            }
            else if (args[1] != "-b")
            {
                try
                {
                  SimulateDynamicHarvest(new ArraySegment<string>(File.ReadAllLines(args[1])));
                }
                catch (Exception e)
                {
                    IfDebugging(e);
                    Console.WriteLine("The specified file either does not exist or was improperly formatted.");
                }
            }
            else if (args.Length >= 3 && args[1] == "-b")
            {
                Batch(args[2], SimulateDynamicHarvest);
            }
            else
            {
                DisplayNewStablePopUsage(); // ??????????????????????????????????????
            }

        }

        private static void SimulateDynamicHarvest(ArraySegment<string> specs)
        {
            StablePopulation sPop; 
            decimal initRate;
            decimal linearRate;
            decimal expoRate;
            int? rngSeed;
            decimal? juvMax = null;
            decimal? primeMax = null;
            string outfile;
            IEnumerable<decimal> minAges;
            IEnumerable<decimal> femaleBiases = null;

            try
            {
                Dictionary<string, string> sorter = GetSorter(':', specs);

                string[] sPopInfo = sorter["StablePop"].Split(' ');
                sPop = StablePopulation.LoadFromFile(sPopInfo[0], sPopInfo[1], sPopInfo[2]);
                initRate = decimal.Parse(sorter["InitialHarvestRate"]);
                linearRate = sorter.ContainsKey("LinearIncreaseRate") ? decimal.Parse(sorter["LinearIncreaseRate"]) : 0m;
                expoRate = sorter.ContainsKey("ExpoIncreaseRate") ? decimal.Parse(sorter["ExpoIncreaseRate"]) : 0m;
                rngSeed = sorter.ContainsKey("RNGSeed") ? (int?)int.Parse(sorter["RNGSeed"]) : null;
                outfile = sorter["OutputFileName"];

                if (sorter.ContainsKey("FemaleProportion"))
                {
                    femaleBiases = sorter["FemaleProportion"].Split(' ').Select(s => decimal.Parse(s));
                }

                minAges = sorter["MinimumAgeHunted"].Split(' ').Select(s => decimal.Parse(s));

                if (sorter.ContainsKey("AgeGroupBreaks"))
                {
                    string[] ageBreaks = sorter["AgeGroupBreaks"].Split(' ');
                    juvMax = (decimal?)decimal.Parse(ageBreaks[0]);
                    primeMax = (decimal?)decimal.Parse(ageBreaks[1]);
                }
            }
            catch (Exception e)
            {
                IfDebugging(e);
                Console.WriteLine("Simulation could not be created. Please check the specification file for errors.");
                return;
            }

            if (femaleBiases.Count() > 0)
            {
                foreach (decimal femBias in femaleBiases)
                {
                    foreach (decimal minAge in minAges)
                    {
                        string simOut = string.Format("{0}{1};{2}.txt", outfile, femBias.ToString().Replace('.', '_'), minAge.ToString().Replace('.', '_'));
                        SinglePopulationSexBiasedSimulation sim = new SinglePopulationSexBiasedSimulation(sPop, initRate, linearRate, expoRate, femBias, minAge, simOut, rngSeed);
                        if (juvMax.HasValue && primeMax.HasValue)
                        {
                            sim.SetTernaryAgeGroups(juvMax.Value, primeMax.Value);
                        }
                        SimulationEnding se = sim.Run();
                        Console.WriteLine(se);
                    }
                }
            }
            else
            {
                foreach (decimal minAge in minAges)
                {
                    string simOut = string.Format("{0}_U;{1}.txt", outfile, minAge.ToString().Replace('.', '_'));
                    SinglePopulationSimulation sim = new SinglePopulationSimulation(sPop, initRate, linearRate, expoRate, minAge, simOut, rngSeed);
                    if (juvMax.HasValue && primeMax.HasValue)
                    {
                        sim.SetTernaryAgeGroups(juvMax.Value, primeMax.Value);
                    }
                    SimulationEnding se = sim.Run();
                    Console.WriteLine(se);
                }
            }
        }



        private static void DisplayDynHarvestUsage()
        {
            throw new NotImplementedException();
        }

        private static void WriteDynHarvestTemplate(string filename)
        {
            using (StreamWriter stw = new StreamWriter(filename))
            {
                stw.WriteLine(@"StablePop:              # The stable population to use; format is ParameterSet Size Timesteps
OutputFileName:         # Tab-delimited file type for results. If the file exists, it will be overwritten.
MinimumAgeHunted:       # In years. Can be fractional (e.g. 1.5)
InitialHarvestRate:     # The proportion of the current population to kill per year when the simulation starts.
LinearIncreaseRate:     # (Use this or ExpoIncreaseRate. Delete the other.) The proportion to be added to the harvest rate each year.
ExpoIncreaseRate:       # (Use this or LinearIncreaseRate. Delete the other.) The annual exponential increase in the harvest rate.
FemaleProportion:       # (Optional; delete this row if not used.) The relative frequency of females among the killed animals.
AgeGroupBreaks:         # (Optional; delete this row if now used.) The upper age limits for juvenile and prime animals; format is JuvMax PrimeMax
RNGSeed:                # (Optional; delete this row if not used.) If provided, this will be deterministically modified for runs after the first.");        
            }
        }

        
        private static void MaxHarvest(string[] args)
        {
            throw new NotImplementedException();
        }


        private static void StablePop(string[] args)
        {
            if (args.Length < 2 || args[1] == "-h" || args[1] == "--help")
            {
                DisplayNewStablePopUsage();
                return;
            }

            if (args[1] == "-t")
            {
                WriteStablePopFormat(args[2]);
            }
            else if (args[1] != "-b")   // args[1] should be filename in this case
            {
                try
                {
                    NewStablePop(new ArraySegment<string>(File.ReadAllLines(args[1])));
                }
                catch (Exception e)
                {
                    IfDebugging(e);
                    Console.WriteLine("The specified file either does not exist or was improperly formatted.");
                }
            }
            else if (args.Length >= 3 && args[1] == "-b")
            {
                Batch(args[2], NewStablePop);
            }
            else
            {
                DisplayNewStablePopUsage();
            }
        }
        
        private static void WriteStablePopFormat(string filename)
        {
            using (StreamWriter stw = new StreamWriter(filename))
            {
                stw.WriteLine($@"ParameterSet: 
Size: 
TimeSteps: ");
            }
        }



        private static void BatchStable(string filename)
        {
            string[] allLines = File.ReadAllLines(filename);
            int start = 0;
            int current = 0;
            while (current < allLines.Length)
            {
                if (allLines[current] != "*")
                {
                    current++;
                }
                else
                {
                    NewStablePop(new ArraySegment<string>(allLines, start, current - start));
                    current++;
                    start = current;
                }
            }
            if (start < allLines.Length)
            {
                NewStablePop(new ArraySegment<string>(allLines, start, current - start));
            }
        }

        private static void Batch(string batchFile, Action<ArraySegment<string>> batchCall)
        {
            string[] allLines = File.ReadAllLines(batchFile);
            int start = 0;
            int current = 0;
            while (current < allLines.Length)
            {
                if (allLines[current] != "*")
                {
                    current++;
                }
                else
                {
                    batchCall(new ArraySegment<string>(allLines, start, current - start));
                    current++;
                    start = current;
                }
            }
            if (start < allLines.Length)
            {
                batchCall(new ArraySegment<string>(allLines, start, current - start));
            }
        }

        private static void NewStablePop(ArraySegment<string> specs)
        {
            try
            {
                Dictionary<string, string> sorter = GetSorter(':', specs);
                StablePopulation sPop = new StablePopulation(ParameterSet.LoadFromFile(sorter["ParameterSet"]), int.Parse(sorter["Size"]), int.Parse(sorter["TimeSteps"]));  /// HERE
                if (!sPop.Stabilized)
                {
                    Console.WriteLine("Population {0} did not stabilize successfully at size {1} and {2} time-steps per year.", sPop.PSet.ModelName, sPop.Size, sPop.TimeSteps);
                }
            }
            catch (Exception e)
            {
                IfDebugging(e);
                Console.WriteLine("Something didn't work. Please check the specification file for errors.");
            }
        }

        private static void DisplayNewStablePopUsage()
        {
            Console.WriteLine(@"
stablepop -n usage:
    stablepop -n [parameterset] [size] <-s|-v>
    
        parameterset    an existing parameter set; use modparam -L to list existing sets or modparam -n [name] to create a new one
        size            an integer value indicating the average size of the stable population to be created
        -s              (optional) silent; no output
        -v              (optional) verbose; additional details in output
");
        }

        private static void ParamSet(string[] args)
        {
            if (args.Length < 2 || !"-n-d-t-L".Contains(args[1]))
            {
                Console.WriteLine(@"
modparam usage:
    -h          display this help message
    -n [name]   create new parameter set named [name]
    -e [name]   edit existing parameter set named [name]
    -L          list available parameter sets
    -t [name]   create a template file for a parameter set named [name]
");
            }
            else if (args.Length == 2)
            {
                if (args[1] == "-L")
                {
                    ListParamSets();
                }
            }
            else if (args.Length >= 3)
            {
                switch (args[1]) {
                    case "-n":
                        NewParameterSetFromUser(args[2]);
                        break;
                    case "-L":
                        DisplayExistingParameterSet(args[2]);
                        break;
                    case "-t":
                        WriteParameterSetTemplate(args[2]);
                        break;
                }
            }

        }

        private static void WriteParameterSetTemplate(string name)
        {
            using (StreamWriter stw = new StreamWriter(name + "_pset.txt"))
            {
                stw.WriteLine($@"Model Name: {name}
Pregnancy Rate: 
Mean Offspring: 
Reproductive Age: 
Adult Mortality Onset Age: 
Maximum Age: 
Base Juvenile Mortality Rate For Females: 
Base Juvenile Mortality Rate For Males: 
Natural Mortality Rate For Adult Females: 
Natural Mortality Rate For Adult Males: ");
            }
        }

        private static void ListStablePops()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string file in Directory.EnumerateFiles(".", "*_spop.bin"))
            {
                sb.AppendLine("\t" + file.Substring(2, file.IndexOf("_spop.bin") - 2));
            }
            Console.WriteLine();
            Console.WriteLine(sb.Length > 0 ? "Available stable populations:" : "No stable populations were found. Use stablepop -n [parameterset] [size] to create a new stable population");
            Console.WriteLine(sb.ToString());
        }

        private static void ListParamSets()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string file in Directory.EnumerateFiles(".","*_pset.txt"))
            {
                sb.AppendLine("\t" + file.Substring(2, file.IndexOf("_pset.txt") - 2));
            }
            Console.WriteLine();
            Console.WriteLine(sb.Length > 0 ? "Available parameter sets:" : "No parameter sets were found. Use param_set -n/-t [name] to create a new parameter set");
            Console.WriteLine(sb.ToString());
        }

        private static void EditExistingParameterSet(string pSetName)
        {
            if (DisplayExistingParameterSet(pSetName, true))
            {
                NewParameterSetFromUser(pSetName);
            }
        }

        private static bool DisplayExistingParameterSet(string pSetName, bool forEdit=false)
        {
            ParameterSet pSet = ParameterSet.LoadFromFile(pSetName);
            if (pSet == null)
            {
                Console.WriteLine("No parameter set named {0} exists.", pSetName);
                return false;
            }
            if (forEdit)
            {
                Console.WriteLine("Current Model Parameters");
                Console.WriteLine("------------------------");
            }
            Console.WriteLine(pSet.ToString());
            Console.WriteLine();
            return true;
        }

 /*       private static bool DisplayExistingStablePop(string sPopName)
        {
            StablePopulation sPop = StablePopulation.LoadFromFile(sPopName);
            if (sPop != null)
            {
                Console.WriteLine(sPop);
                return true;
            }
            return false;
        }*/



        private static void NewParameterSetFromUser(string pSetName)
        {
            ParameterSet ps = new ParameterSet();
            ps.ModelName = pSetName;
            Console.WriteLine("Please enter parameter values for {0}. (Press ctrl-z and enter to cancel)", pSetName);
            Console.WriteLine();

            try
            {
                ps.PregnancyRate = GetDecimalParamFromUser("Pregnancy Rate", x => (x >= .01m && x <= 1m), "between .01 and 1 (inclusive)");
                ps.MeanOffspring = GetDecimalParamFromUser("Mean Offspring", x => (x >= 1m), "greater than or equal to 1.0");
                ps.AgeOfFirstRepro = GetIntParamFromUser("Female Age at First Reproduction (in years)", i => (i >= 1), "greater than zero");
                ps.AgeOnsetAdultMortality = GetDecimalParamFromUser("Age of Onset of Adult Mortality (in years; can be fractional)", x => (x >= 0m), "greater than zero");
                ps.AgeMaximum = GetIntParamFromUser("Maximum Age (in years)", i => (i >= 1), "greater than zero");
                ps.FemaleBaseJuvMortRate = GetDecimalParamFromUser("Base Juvenile Mortality Rate (Female)", x => (x >= .001m && x < 1m), "at least .001 and less than 1.0");
                ps.MaleBaseJuvMortRate = GetDecimalParamFromUser("Base Juvenile Mortality Rate (Male)", x => (x >= .001m && x < 1m), "at least .001 and less than 1.0");
                ps.FemaleAdultNatMortRate = GetDecimalParamFromUser("Adult Natural Mortality Rate (Female)", x => (x >= .001m && x < 1m), "at least .001 and less than 1.0");
                ps.MaleAdultNatMortRate = GetDecimalParamFromUser("Adult Natural Mortality Rate (Male)", x => (x >= .001m && x < 1m), "at least .001 and less than 1.0");
            }
            catch (Exception e)
            {
                if (e.Message == "Canceled")
                {
                    Console.WriteLine();
                    Console.WriteLine("Parameter set entry canceled.");
                    return;
                }
                else
                {
                    throw e;
                }
            }

            ps.Save();
            Console.WriteLine();
            Console.WriteLine("Parameter set saved");
            Console.WriteLine();
        }

    }
}

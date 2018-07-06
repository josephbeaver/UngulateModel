using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using static BeaverUtils.ModelUtils;
using static BeaverUtils.ConsoleUtils;

namespace UngulateModel
{
    [Serializable]
    class ParameterSet
    {
        public string ModelName { get; set; }
        public decimal PregnancyRate { get; set; }
        public decimal MeanOffspring { get; set; }
        public int AgeOfFirstRepro { get; set; }
        public decimal AgeOnsetAdultMortality { get; set; }
        public int AgeMaximum { get; set; }
        public decimal FemaleBaseJuvMortRate { get; set; }
        public decimal MaleBaseJuvMortRate { get; set; }
        public decimal FemaleAdultNatMortRate { get; set; }
        public decimal MaleAdultNatMortRate { get; set; }       


        public static ParameterSet LoadFromFile(string pSetName)
        {
            try
            {
                Dictionary<string, string> sorter = new Dictionary<string, string>();
                string[] lines = File.ReadAllLines(pSetName + "_pset.txt");
                foreach (string line in lines)
                {
                    string[] elements = line.Split(new string[] { ": "}, StringSplitOptions.RemoveEmptyEntries);
                    sorter[elements[0].Replace(" ", "")] = elements[1];
                }

                ParameterSet ps = new ParameterSet
                {
                    ModelName = sorter["ModelName"],
                    PregnancyRate = decimal.Parse(sorter["PregnancyRate"]),
                    MeanOffspring = decimal.Parse(sorter["MeanOffspring"]),
                    AgeOfFirstRepro = int.Parse(sorter["ReproductiveAge"]),
                    AgeOnsetAdultMortality = decimal.Parse(sorter["AdultMortalityOnsetAge"]),
                    AgeMaximum = int.Parse(sorter["MaximumAge"]),
                    FemaleBaseJuvMortRate = decimal.Parse(sorter["BaseJuvenileMortalityRateForFemales"]),
                    MaleBaseJuvMortRate = decimal.Parse(sorter["BaseJuvenileMortalityRateForMales"]),
                    FemaleAdultNatMortRate = decimal.Parse(sorter["NaturalMortalityRateForAdultFemales"]),
                    MaleAdultNatMortRate = decimal.Parse(sorter["NaturalMortalityRateForAdultMales"])
                };
                
                return ps;
            }
            catch (Exception e)
            {
                IfDebugging(e);
                return null;
            }
        }

        public void Save()
        {
            using (StreamWriter stw = new StreamWriter(ModelName + "_pset.txt"))
            {
                stw.WriteLine(this.ToString());
            }
        }

        public override string ToString()
        {
            return $@"Model Name: {ModelName}
Pregnancy Rate: {PregnancyRate}
Mean Offspring: {MeanOffspring}
Reproductive Age: {AgeOfFirstRepro}
Adult Mortality Onset Age: {AgeOnsetAdultMortality}
Maximum Age: {AgeMaximum}
Base Juvenile Mortality Rate For Females: {FemaleBaseJuvMortRate}
Base Juvenile Mortality Rate For Males: {MaleBaseJuvMortRate}
Natural Mortality Rate For Adult Females: {FemaleAdultNatMortRate}
Natural Mortality Rate For Adult Males: {MaleAdultNatMortRate}";
        }
    }
}

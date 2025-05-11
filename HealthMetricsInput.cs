using System;

namespace VirtualHealthAPI
{
    public class HealthMetricsInput
    {
        //clinical data
       
        public double Cholesterol { get; set; }//2093-3
        public double Hba1c { get; set; }//4548-4
        public double Hdl { get; set; }//2085-9
        public double Ldl { get; set; }//2089-1
        public double Triglycerides { get; set; }//2571-8
        public double Hemoglobin { get; set; }//718-7
        public double WbcCount { get; set; }//6690-2
        public double RbcCount { get; set; }//789-8
        public double VitaminD { get; set; }//1988-5
        public double VitaminB12 { get; set; }//2132-9
        public double Iron { get; set; }//2498-4
        public double PlateletCount { get; set; }//777-3
        public double Tsh { get; set; }//3016-3
        public double T3 { get; set; }//3016-3
        public double T4 { get; set; }//3024-7
        public int FamilyHistory { get; set; }//0/1
        public int Smoking { get; set; }//0/1
        public int Alchohal { get; set; }//0/1

        /// wearable factors
        public int FastingGlucose { get; set; }//1558-6
        public double Pulse { get; set; } // 8867-4
        public double SystolicBp { get; set; } // 8480-6
        public double DiastolicBp { get; set; }// 8462-4
        public double Spo2 { get; set; }// 59408-5
        public double BodyTemp { get; set; }// 8310-5
        public double CaloriesBurned { get; set; }// 59267-0
        public double Steps { get; set; }
        public double StressLevel { get; set; }
        public double RespiratoryRate { get; set; } // 9279-1
        public double Vo2Max { get; set; } // 40590-2  
        public double HeartRateVariability { get; set; } // 8310-5
        public double Hrv { get; set; } // 8926-0 heart rate variability
        public double? SleepDuration { get; set; } // 27113001

    }
}

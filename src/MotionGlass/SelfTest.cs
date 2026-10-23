using System.Text.Json;
using System.Security.Cryptography;

namespace MotionGlass;

internal static class SelfTest
{
    internal static void Run(string[] args)
    {
        string folder=args.SkipWhile(a=>a!="--self-test").Skip(1).FirstOrDefault() ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(folder);
        var checks=new List<string>();
        var movingOutputHashes=new Dictionary<string,string>();
        try
        {
            using(var gpu=new GraphicsPipeline(null,128,96,128,96,true))
            {
                byte[] frame=new byte[128*96*4];
                for(int i=0;i<frame.Length;i+=4) { frame[i]=30; frame[i+1]=50; frame[i+2]=70; frame[i+3]=255; }
                foreach(BlurMode mode in Enum.GetValues<BlurMode>())
                {
                    gpu.Reset(); gpu.UploadDesktop(frame);
                    for(int f=0;f<4;++f) gpu.Process(new Settings(mode,50),1f/60,1,Point.Empty,Size.Empty,false);
                    byte[] output=gpu.ReadResult();
                    if(output.Where((v,i)=>Math.Abs(v-frame[i])>2).Any()) throw new Exception($"Static image changed in {mode}.");
                    checks.Add($"PASS: {mode} preserves a static image");
                }
                var outputs=new List<byte[]>();
                foreach(BlurMode mode in Enum.GetValues<BlurMode>())
                {
                    gpu.Reset();
                    for(int f=0;f<6;++f)
                    {
                        var moving=(byte[])frame.Clone();
                        for(int y=30;y<60;++y) for(int x=15+f*7;x<35+f*7;++x)
                        { int i=(y*128+x)*4; moving[i]=210; moving[i+1]=230; moving[i+2]=250; }
                        gpu.UploadDesktop(moving); gpu.Process(new Settings(mode,65),1f/60,1,Point.Empty,Size.Empty,false);
                    }
                    byte[] output=gpu.ReadResult(); outputs.Add(output);
                    movingOutputHashes[mode.ToString()]=Convert.ToHexStringLower(SHA256.HashData(output));
                    checks.Add($"PASS: {mode} renders moving content");
                }
                for(int i=0;i<outputs.Count;i++) for(int j=i+1;j<outputs.Count;j++)
                    if(outputs[i].SequenceEqual(outputs[j])) throw new Exception($"{(BlurMode)i} and {(BlurMode)j} produced identical motion output.");
                checks.Add("PASS: all five modes produce distinct moving output");
                foreach(BlurMode mode in new[] {BlurMode.Velocity,BlurMode.AccumulationMax,BlurMode.AccumulationMix})
                {
                    gpu.UploadDesktop(frame); gpu.Process(new Settings(mode,0),1f/60,1,Point.Empty,Size.Empty,false);
                    if(gpu.ReadResult().Where((v,i)=>Math.Abs(v-frame[i])>2).Any()) throw new Exception("0% changed the image.");
                }
                checks.Add("PASS: 0% passes through unmodified pixels for adjustable modes");
            }
#if MOTIONGLASS_DEVELOPMENT_TESTS
            using(var ui=new MainForm(true,false))
            {
                ui.Show(); Application.DoEvents(); ui.ValidateControlRules();
                Pump(250);
                ui.CapturePreview(Path.Combine(folder,"MotionGlass-preview.png")); ui.Close();
                checks.Add("PASS: controls enable and disable correctly in every mode");
            }
#endif
            File.WriteAllText(Path.Combine(folder,"self-test.json"),JsonSerializer.Serialize(new { passed=true,checks,movingOutputHashes },new JsonSerializerOptions { WriteIndented=true }));
            Environment.ExitCode=0;
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(folder,"self-test.json"),JsonSerializer.Serialize(new { passed=false,checks,error=ex.ToString() },new JsonSerializerOptions { WriteIndented=true }));
            Environment.ExitCode=1;
        }
    }
#if MOTIONGLASS_DEVELOPMENT_TESTS
    internal static void Desktop(string[] args)
    {
        string folder=args.SkipWhile(a=>a!="--desktop-test").Skip(1).FirstOrDefault() ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(folder);
        var checks=new List<string>();
        using var ui=new MainForm(true,false);
        try
        {
            ui.Show(); Pump(250); ui.TestPower(true);
            foreach(BlurMode mode in Enum.GetValues<BlurMode>())
            {
                ui.Change(new Settings(mode,50,false));
                Pump(2200);
                string status=ui.TestStatus;
                checks.Add($"{mode}: {status}");
                if(!status.StartsWith("Live")) throw new Exception(status);
            }
            ui.Change(new Settings(BlurMode.Velocity,0)); Pump(150);
            if(!ui.TestStatus.StartsWith("Off")) throw new Exception("0% did not stop the effect.");
            checks.Add("PASS: 0% suspends the live overlay");
            ui.TestPower(false); Pump(100);
            if(!ui.TestStatus.StartsWith("Off")) throw new Exception("Power off did not stop the effect.");
            checks.Add("PASS: power off hides the overlays");
            File.WriteAllText(Path.Combine(folder,"desktop-test.json"),JsonSerializer.Serialize(new {passed=true,checks},new JsonSerializerOptions {WriteIndented=true}));
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(folder,"desktop-test.json"),JsonSerializer.Serialize(new {passed=false,checks,error=ex.ToString()},new JsonSerializerOptions {WriteIndented=true}));
            Environment.ExitCode=1;
        }
        finally { ui.TestPower(false); ui.Close(); }
    }
    private static void Pump(int ms)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<ms) { Application.DoEvents(); Thread.Sleep(8); }
    }
#endif
}

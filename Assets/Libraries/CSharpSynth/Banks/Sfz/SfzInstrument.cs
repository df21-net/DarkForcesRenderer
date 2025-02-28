﻿using CSharpSynth.Synthesis;
using CSharpSynth.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CSharpSynth.Banks.Sfz {
  public class SfzInstrument : Instrument {
    //--Variables
    private SfzRegion[] regions;   //the complete list of regions
    private byte[] noteMap;        //maps the midi note to the correct region (wont work for complex sfz)
    private bool allSamplesHaveDualChannels;
    //--Public Methods
    public SfzInstrument(string Instrumentfile, int sampleRate, InstrumentBank bank)
        : base() {
      this.SampleRate = sampleRate;
      //UnitySynth
      //ReadFromStream(File.Open(Instrumentfile, FileMode.Open), Path.GetDirectoryName(Instrumentfile) + "\\", bank);
      TextAsset instrumentFile = Resources.Load(Instrumentfile) as TextAsset;
      Stream instrumentStream = new MemoryStream(instrumentFile.bytes);
      //Debug.Log(Instrumentfile);
      this.ReadFromStream(instrumentStream, Path.GetDirectoryName(Instrumentfile) + "/", bank);

      this.CreateKeyMap();
      this.Name = Path.GetFileNameWithoutExtension(Instrumentfile);
    }
    public override bool AllSamplesSupportDualChannel() => this.allSamplesHaveDualChannels;
    public override int GetAttack(int note) => this.regions[this.noteMap[note]].Attack;
    public override int GetRelease(int note) => this.regions[this.noteMap[note]].Release;
    public override int GetDecay(int note) => this.regions[this.noteMap[note]].Decay;
    public override int GetHold(int note) => this.regions[this.noteMap[note]].Hold;
    public override void EnforceSampleRate(int sampleRate) {
      //The SFZ instrument automatically enforces the SampleRate in
      //ParseInstrumentData(...) so for the most part this will not be needed...
      if (sampleRate == this.SampleRate) {
        return;
      }
      //Enforce SampleRate on Parameters
      for (int x = 0; x < this.regions.Length; x++) {
        //Sample s = this.SampleList[this.regions[x].SampleIndex];
        float factor = (sampleRate / this.SampleRate);
        this.regions[x].LoopEnd = (int)(this.regions[x].LoopEnd * factor);
        this.regions[x].LoopStart = (int)(this.regions[x].LoopStart * factor);
        this.regions[x].Offset = (int)(this.regions[x].Offset * factor);
        this.regions[x].Attack = (int)(this.regions[x].Attack * factor);
        this.regions[x].Release = (int)(this.regions[x].Release * factor);
        this.regions[x].Decay = (int)(this.regions[x].Decay * factor);
        this.regions[x].Hold = (int)(this.regions[x].Hold * factor);
      }
      //Enforce SampleRate on Samples
      for (int x = 0; x < this.SampleList.Length; x++) {
        Sample s = this.SampleList[x];
        float factor = sampleRate / (float)s.SampleRate;
        if (factor != 1.0f) {
          s.SetAllSampleData(WaveHelper.ReSample(sampleRate, s.SampleRate, s.GetAllSampleData()));
          s.SampleRate = sampleRate;
        }
      }
      this.SampleRate = sampleRate;
    }
    public override float GetSampleAtTime(int note, int channel, int synthSampleRate, ref double time) {
      int originalSampleRate = synthSampleRate;
      SfzRegion r = this.regions[this.noteMap[note]];
      Sample neededSample = this.SampleList[r.SampleIndex];
      //Calculate SampleRate from pitch
      synthSampleRate = (int)(synthSampleRate * Math.Pow(2.0, ((note - r.Root) + r.Tune) / 12.0));
      int Currentsample = (int)(time / (1.0 / synthSampleRate));
      if (Currentsample > r.LoopEnd) {
        if (r.LoopMode == 0) {
          time -= (1.0 / originalSampleRate);
          return 0.0f;
        } else {
          Currentsample = r.LoopStart;
          time = r.LoopStart * (1.00 / synthSampleRate);
        }
      }
      return neededSample.GetSample(channel, Currentsample);
    }
    //--Private Methods
    private void ReadFromStream(Stream InstrumentStream, string path, InstrumentBank bank) {
      StreamReader reader = new StreamReader(InstrumentStream);
      List<string> text = new List<string>();
      while (reader.Peek() > -1) {
        text.Add(reader.ReadLine());
      }

      reader.Dispose();
      InstrumentStream.Dispose();
      this.ParseInstrumentData(text.ToArray(), path, bank);
    }
    private void ParseInstrumentData(string[] text, string InstrumentPath, InstrumentBank bank) {
      this.allSamplesHaveDualChannels = true;
      SfzRegion Group = new SfzRegion();
      bool[] groupValues = new bool[21];
      List<Sample> Samples = new List<Sample>();
      List<string> SampleNames = new List<string>();
      List<SfzRegion> Regions = new List<SfzRegion>();
      for (int x = 0; x < text.Length; x++) {
        string line = text[x].Trim();
        if (line != "") {
          switch (line.Substring(0, 1).ToLower()) {
            case "<":
              if (line == "<group>") {
                while (text[x] != "") {
                  x++;
                  if (x >= text.Length) {
                    break;
                  }

                  string[] Rvalue = text[x].Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                  if (Rvalue.Length == 2) {
                    switch (Rvalue[0]) {
                      case "sample":
                        Rvalue[1] = Path.GetFileNameWithoutExtension(Rvalue[1]);
                        groupValues[0] = true;
                        if (SampleNames.Contains(Rvalue[1]))//its in the local list so no need to check the master list
                        {
                          Group.SampleIndex = SampleNames.IndexOf(Rvalue[1]);
                        } else {
                          SampleNames.Add(Rvalue[1]);
                          //check if the sample is in the master list.
                          int sampNum = bank.SampleNameList.IndexOf(Rvalue[1]);
                          Sample s;
                          if (sampNum > -1)//its in the list
                          {
                            s = bank.SampleList[sampNum];
                          } else//its not in the list
                            {
                            s = new Sample(InstrumentPath + "SAMPLES/" + Rvalue[1] + ".wav");
                            bank.SampleList.Add(s);
                            bank.SampleNameList.Add(Rvalue[1]);
                          }
                          if (s.IsDualChannel == false) {
                            this.allSamplesHaveDualChannels = false;
                          }

                          Samples.Add(s);
                          Group.SampleIndex = SampleNames.Count - 1;
                        }
                        break;
                      case "hikey":
                        Group.HiNote = (byte)int.Parse(Rvalue[1]);
                        groupValues[1] = true;
                        break;
                      case "lokey":
                        Group.LoNote = (byte)int.Parse(Rvalue[1]);
                        groupValues[2] = true;
                        break;
                      case "loop_start":
                        Group.LoopStart = int.Parse(Rvalue[1]);
                        groupValues[3] = true;
                        break;
                      case "loop_end":
                        Group.LoopEnd = int.Parse(Rvalue[1]);
                        groupValues[4] = true;
                        break;
                      case "tune":
                        Group.Tune = int.Parse(Rvalue[1]) / 100.0f;
                        groupValues[5] = true;
                        break;
                      case "pitch_keycenter":
                        Group.Root = int.Parse(Rvalue[1]);
                        groupValues[6] = true;
                        break;
                      case "volume":
                        Group.Volume = float.Parse(Rvalue[1]);
                        groupValues[7] = true;
                        break;
                      case "loop_mode":
                        groupValues[8] = true;
                        switch (Rvalue[1]) {
                          case "loop_continuous":
                            Group.LoopMode = 1;
                            break;
                          case "loop_sustain":
                            Group.LoopMode = 2;
                            break;
                          default:
                            Group.LoopMode = 0;
                            break;
                        }
                        break;
                      case "ampeg_release":
                        groupValues[9] = true;
                        Group.Release = SynthHelper.GetSampleFromTime(this.SampleRate, float.Parse(Rvalue[1]));
                        break;
                      case "ampeg_attack":
                        groupValues[10] = true;
                        Group.Attack = SynthHelper.GetSampleFromTime(this.SampleRate, float.Parse(Rvalue[1]));
                        break;
                      case "ampeg_decay":
                        groupValues[11] = true;
                        Group.Decay = SynthHelper.GetSampleFromTime(this.SampleRate, float.Parse(Rvalue[1]));
                        break;
                      case "ampeg_hold":
                        groupValues[12] = true;
                        Group.Hold = SynthHelper.GetSampleFromTime(this.SampleRate, float.Parse(Rvalue[1]));
                        break;
                      case "lovel":
                        groupValues[13] = true;
                        Group.LoVelocity = (byte)int.Parse(Rvalue[1]);
                        break;
                      case "hivel":
                        groupValues[14] = true;
                        Group.HiVelocity = (byte)int.Parse(Rvalue[1]);
                        break;
                      case "lochan":
                        groupValues[15] = true;
                        Group.LoChannel = (byte)(int.Parse(Rvalue[1]) - 1);
                        break;
                      case "hichan":
                        groupValues[16] = true;
                        Group.HiChannel = (byte)(int.Parse(Rvalue[1]) - 1);
                        break;
                      case "key":
                        Group.Root = int.Parse(Rvalue[1]);
                        groupValues[6] = true;
                        Group.HiNote = (byte)Group.Root;
                        groupValues[1] = true;
                        Group.LoNote = (byte)Group.Root;
                        groupValues[2] = true;
                        break;
                      case "offset":
                        groupValues[17] = true;
                        Group.Offset = int.Parse(Rvalue[1]);
                        break;
                      case "pan":
                        groupValues[18] = true;
                        Group.Pan = float.Parse(Rvalue[1]);
                        break;
                      case "effect1":
                        groupValues[19] = true;
                        Group.Effect1 = float.Parse(Rvalue[1]);
                        break;
                      case "effect2":
                        groupValues[20] = true;
                        Group.Effect2 = float.Parse(Rvalue[1]);
                        break;
                      default:
                        break;
                    }
                  }
                }
              } else if (line == "<region>") {
                SfzRegion r = new SfzRegion();
                while (text[x] != "") {
                  x++;
                  if (x >= text.Length) {
                    break;
                  }

                  string[] Rvalue = text[x].Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                  if (Rvalue.Length == 2) {
                    switch (Rvalue[0]) {
                      case "sample":
                        Rvalue[1] = Path.GetFileNameWithoutExtension(Rvalue[1]);//remove ending .wav
                        if (SampleNames.Contains(Rvalue[1]))//its in the local list so no need to check the master list
                        {
                          r.SampleIndex = SampleNames.IndexOf(Rvalue[1]);
                        } else {
                          SampleNames.Add(Rvalue[1]);
                          //check if the sample is in the master list.
                          int sampNum = bank.SampleNameList.IndexOf(Rvalue[1]);
                          Sample s;
                          if (sampNum > -1)//its in the list
                          {
                            s = bank.SampleList[sampNum];
                          } else//its not in the list
                            {
                            s = new Sample(InstrumentPath + "SAMPLES/" + Rvalue[1] + ".wav");
                            bank.SampleList.Add(s);
                            bank.SampleNameList.Add(Rvalue[1]);
                          }
                          if (s.IsDualChannel == false) {
                            this.allSamplesHaveDualChannels = false;
                          }

                          Samples.Add(s);
                          r.SampleIndex = SampleNames.Count - 1;
                        }
                        break;
                      case "hikey":
                        r.HiNote = (byte)int.Parse(Rvalue[1]);
                        break;
                      case "lokey":
                        r.LoNote = (byte)int.Parse(Rvalue[1]);
                        break;
                      case "loop_start":
                        r.LoopStart = int.Parse(Rvalue[1]);
                        break;
                      case "loop_end":
                        r.LoopEnd = int.Parse(Rvalue[1]);
                        break;
                      case "tune":
                        r.Tune = int.Parse(Rvalue[1]) / 100.0f;
                        break;
                      case "pitch_keycenter":
                        r.Root = int.Parse(Rvalue[1]);
                        break;
                      case "volume":
                        r.Volume = float.Parse(Rvalue[1]);
                        break;
                      case "loop_mode":
                        switch (Rvalue[1]) {
                          case "loop_continuous":
                            r.LoopMode = 1;
                            break;
                          case "loop_sustain":
                            r.LoopMode = 2;
                            break;
                          default:
                            r.LoopMode = 0;
                            break;
                        }
                        break;
                      case "ampeg_release":
                        r.Release = SynthHelper.GetSampleFromTime(this.SampleRate, float.Parse(Rvalue[1]));
                        break;
                      case "ampeg_attack":
                        r.Attack = SynthHelper.GetSampleFromTime(this.SampleRate, float.Parse(Rvalue[1]));
                        break;
                      case "ampeg_decay":
                        r.Decay = SynthHelper.GetSampleFromTime(this.SampleRate, float.Parse(Rvalue[1]));
                        break;
                      case "ampeg_hold":
                        r.Hold = SynthHelper.GetSampleFromTime(this.SampleRate, float.Parse(Rvalue[1]));
                        break;
                      case "lovel":
                        r.LoVelocity = (byte)int.Parse(Rvalue[1]);
                        break;
                      case "hivel":
                        r.HiVelocity = (byte)int.Parse(Rvalue[1]);
                        break;
                      case "lochan":
                        r.LoChannel = (byte)(int.Parse(Rvalue[1]) - 1);
                        break;
                      case "hichan":
                        r.HiChannel = (byte)(int.Parse(Rvalue[1]) - 1);
                        break;
                      case "key":
                        r.Root = int.Parse(Rvalue[1]);
                        r.HiNote = (byte)r.Root;
                        r.LoNote = (byte)r.Root;
                        break;
                      case "offset":
                        r.Offset = int.Parse(Rvalue[1]);
                        break;
                      case "pan":
                        r.Pan = float.Parse(Rvalue[1]);
                        break;
                      case "effect1":
                        r.Effect1 = float.Parse(Rvalue[1]);
                        break;
                      case "effect2":
                        r.Effect2 = float.Parse(Rvalue[1]);
                        break;
                      default:
                        break;
                    }
                  }
                }
                if (Regions.Contains(r) == false) {
                  Regions.Add(r);
                }
              }
              break;
            default:
              break;
          }
        }
      }
      //Apply group values here
      for (int x = 0; x < Regions.Count; x++) {
        if (groupValues[0] == true) {
          Regions[x].SampleIndex = Group.SampleIndex;
        }

        if (groupValues[1] == true) {
          Regions[x].HiNote = Group.HiNote;
        }

        if (groupValues[2] == true) {
          Regions[x].LoNote = Group.LoNote;
        }

        if (groupValues[3] == true) {
          Regions[x].LoopStart = Group.LoopStart;
        }

        if (groupValues[4] == true) {
          Regions[x].LoopEnd = Group.LoopEnd;
        }

        if (groupValues[5] == true) {
          Regions[x].Tune = Group.Tune;
        }

        if (groupValues[6] == true) {
          Regions[x].Root = Group.Root;
        }

        if (groupValues[7] == true) {
          Regions[x].Volume = Group.Volume;
        }

        if (groupValues[8] == true) {
          Regions[x].LoopMode = Group.LoopMode;
        }

        if (groupValues[9] == true) {
          Regions[x].Release = Group.Release;
        }

        if (groupValues[10] == true) {
          Regions[x].Attack = Group.Attack;
        }

        if (groupValues[11] == true) {
          Regions[x].Decay = Group.Decay;
        }

        if (groupValues[12] == true) {
          Regions[x].Hold = Group.Hold;
        }

        if (groupValues[13] == true) {
          Regions[x].LoVelocity = Group.LoVelocity;
        }

        if (groupValues[14] == true) {
          Regions[x].HiVelocity = Group.HiVelocity;
        }

        if (groupValues[15] == true) {
          Regions[x].LoChannel = Group.LoChannel;
        }

        if (groupValues[16] == true) {
          Regions[x].HiChannel = Group.HiChannel;
        }

        if (groupValues[17] == true) {
          Regions[x].Offset = Group.Offset;
        }

        if (groupValues[18] == true) {
          Regions[x].Pan = Group.Pan;
        }

        if (groupValues[19] == true) {
          Regions[x].Effect1 = Group.Effect1;
        }

        if (groupValues[20] == true) {
          Regions[x].Effect2 = Group.Effect2;
        }
      }
      this.SampleList = Samples.ToArray();
      this.regions = Regions.ToArray();
      //Fix parameters so enforce isn't needed
      for (int x = 0; x < this.regions.Length; x++) {
        Sample s = this.SampleList[this.regions[x].SampleIndex];
        float factor = (this.SampleRate / (float)s.OriginalSampleRate);
        this.regions[x].LoopEnd = (int)(this.regions[x].LoopEnd * factor);
        this.regions[x].LoopStart = (int)(this.regions[x].LoopStart * factor);
        this.regions[x].Offset = (int)(this.regions[x].Offset * factor);
        //Set loopend to end of sample if none is provided
        if (this.regions[x].LoopEnd <= 0) {
          if (this.SampleRate != s.SampleRate) {
            this.regions[x].LoopEnd = (int)(this.SampleList[this.regions[x].SampleIndex].SamplesPerChannel * factor) - 1;
          } else {
            this.regions[x].LoopEnd = this.SampleList[this.regions[x].SampleIndex].SamplesPerChannel - 1;
          }
        }
      }
      //Resample as well
      for (int x = 0; x < this.SampleList.Length; x++) {
        Sample s = this.SampleList[x];
        float factor = this.SampleRate / (float)s.SampleRate;
        if (factor != 1.0f) {
          s.SetAllSampleData(WaveHelper.ReSample(this.SampleRate, s.SampleRate, s.GetAllSampleData()));
          s.SampleRate = this.SampleRate;
        }
      }
    }
    private void CreateKeyMap() {
      this.noteMap = new byte[255];
      for (int x = 0; x < this.regions.Length; x++) {
        for (int x2 = this.regions[x].LoNote; x2 < this.regions[x].HiNote + 1; x2++) {
          this.noteMap[x2] = (byte)x;
        }
      }
    }
  }
}

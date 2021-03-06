﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DM;
using Crestron.SimplSharpPro.DM.Endpoints;
using Crestron.SimplSharpPro.DM.Endpoints.Receivers;

using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.DM
{
    /// <summary>
    /// Represent both a transmitter and receiver pair of the HD-MD-400-C-E / HD-MD-300-C-E / HD-MD-200-C-E kits
    /// </summary>
    public class HdMdxxxCEController : CrestronGenericBaseDevice, IRouting//, IComPorts
    {
        /// <summary>
        /////  DmLite Ports
        ///// </summary>
        //public RoutingOutputPort ToRx { get; private set; }
        //public RoutingInputPort FromTx { get; private set; }

        public RoutingOutputPort HdmiOut { get; private set; }

        public HdMdxxxCE TxRxPair { get; private set; }

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        /// <summary>
        /// The value of the current video source for the HDMI output on the receiver
        /// </summary>
        public IntFeedback VideoSourceFeedback { get; private set; }

        /// <summary>
        /// Indicates if Auto Route is on on the transmitter
        /// </summary>
        public BoolFeedback AutoRouteOnFeedback { get; private set; }

        /// <summary>
        /// Indicates if Priority Routing is on on the transmitter
        /// </summary>
        public BoolFeedback PriorityRoutingOnFeedback { get; private set; }

        /// <summary>
        /// INdicates if the On Screen Display is enabled 
        /// </summary>
        public BoolFeedback InputOnScreenDisplayEnabledFeedback { get; private set; }

        /// <summary>
        /// Indicates if video sync is detected on each of the inputs
        /// </summary>
        public Dictionary<uint, BoolFeedback> SyncDetectedFeedbacks { get; private set; }

        /// <summary>
        /// Indicates if the remote end device is detected
        /// </summary>
        public BoolFeedback RemoteEndDetectedFeedback { get; private set; }

        public RoutingPortCollection<RoutingOutputPort> OutputPorts
        {
            get { return new RoutingPortCollection<RoutingOutputPort> { HdmiOut }; }
        }

        public HdMdxxxCEController(string key, string name, HdMdxxxCE txRxPair)
            :base(key, name, txRxPair)
        {

            TxRxPair = txRxPair;

            RemoteEndDetectedFeedback = new BoolFeedback(() => TxRxPair.RemoteEndDetectedOnFeedback.BoolValue);

            AutoRouteOnFeedback = new BoolFeedback(() => TxRxPair.TransmitterAutoModeOnFeedback.BoolValue);

            PriorityRoutingOnFeedback = new BoolFeedback(() => TxRxPair.PriorityRoutingOnFeedback.BoolValue);

            InputOnScreenDisplayEnabledFeedback = new BoolFeedback(() => TxRxPair.OnScreenDisplayEnabledFeedback.BoolValue);

            InputPorts = new RoutingPortCollection<RoutingInputPort>();

            SyncDetectedFeedbacks = new Dictionary<uint, BoolFeedback>();

            // Add the HDMI input port on the receiver
            InputPorts.Add(new RoutingInputPort(DmPortName.Hdmi, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, 1, this));

            SyncDetectedFeedbacks.Add(1, new BoolFeedback( () => TxRxPair.HdmiInputs[1].VideoDetectedFeedback.BoolValue));

            if(txRxPair is HdMd400CE)
            {
                InputPorts.Add(new RoutingInputPort(DmPortName.HdmiIn1, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, 2, this));
                SyncDetectedFeedbacks.Add(2, new BoolFeedback(() => TxRxPair.HdmiInputs[2].VideoDetectedFeedback.BoolValue));

                InputPorts.Add(new RoutingInputPort(DmPortName.HdmiIn2, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, 3, this));
                SyncDetectedFeedbacks.Add(3, new BoolFeedback(() => TxRxPair.HdmiInputs[3].VideoDetectedFeedback.BoolValue));

                InputPorts.Add(new RoutingInputPort(DmPortName.VgaIn, eRoutingSignalType.Video | eRoutingSignalType.Audio,
                    eRoutingPortConnectionType.Vga, 4, this));
                SyncDetectedFeedbacks.Add(4, new BoolFeedback(() => TxRxPair.VgaInputs[1].VideoDetectedFeedback.BoolValue));

                // Set Ports for CEC
                InputPorts[DmPortName.HdmiIn1].Port = TxRxPair.HdmiInputs[1];
                InputPorts[DmPortName.HdmiIn2].Port = TxRxPair.HdmiInputs[2];
            }
            else if (txRxPair is HdMd300CE)
            {
                InputPorts.Add(new RoutingInputPort(DmPortName.HdmiIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, 2, this));
                SyncDetectedFeedbacks.Add(2, new BoolFeedback(() => TxRxPair.HdmiInputs[2].VideoDetectedFeedback.BoolValue));

                InputPorts.Add(new RoutingInputPort(DmPortName.VgaIn, eRoutingSignalType.Video | eRoutingSignalType.Audio,
                    eRoutingPortConnectionType.Vga, 3, this));
                SyncDetectedFeedbacks.Add(3, new BoolFeedback(() => TxRxPair.VgaInputs[1].VideoDetectedFeedback.BoolValue));

                // Set Ports for CEC
                InputPorts[DmPortName.HdmiIn].Port = TxRxPair.HdmiInputs[1];
            }
            else if (txRxPair is HdMd200CE || txRxPair is HdMd200C1GE)
            {
                InputPorts.Add(new RoutingInputPort(DmPortName.HdmiIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, 2, this));
                SyncDetectedFeedbacks.Add(2, new BoolFeedback(() => TxRxPair.HdmiInputs[2].VideoDetectedFeedback.BoolValue));

                // Set Ports for CEC
                InputPorts[DmPortName.HdmiIn].Port = TxRxPair.HdmiInputs[1];
            }

            //ToRx = new RoutingOutputPort(DmPortName.ToTx, eRoutingSignalType.Audio | eRoutingSignalType.Video,
            //    eRoutingPortConnectionType.DmCat, null, this);

            //FromTx = new RoutingInputPort(DmPortName.FromTx, eRoutingSignalType.Audio | eRoutingSignalType.Video,
            //    eRoutingPortConnectionType.DmCat, null, this);

            HdmiOut = new RoutingOutputPort(DmPortName.HdmiOut, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, null, this);

            OutputPorts[DmPortName.HdmiOut].Port = TxRxPair.HdmiOutputs[1];

            TxRxPair.DMInputChange += new DMInputEventHandler(TxRxPair_DMInputChange);
            TxRxPair.DMOutputChange += new DMOutputEventHandler(TxRxPair_DMOutputChange);
            TxRxPair.DMSystemChange += new DMSystemEventHandler(TxRxPair_DMSystemChange);

            VideoSourceFeedback = new IntFeedback(() => (int)TxRxPair.HdmiOutputs[1].VideoOutFeedback.Number);
        }

        void TxRxPair_DMSystemChange(Switch device, DMSystemEventArgs args)
        {
            if (args.EventId == DMSystemEventIds.RemoteEndDetectedEventId)
                RemoteEndDetectedFeedback.FireUpdate();
            else if (args.EventId == DMSystemEventIds.TransmitterAutoModeOnEventId)
                AutoRouteOnFeedback.FireUpdate();
            else if (args.EventId == DMSystemEventIds.PriorityRoutingOnEventId)
                PriorityRoutingOnFeedback.FireUpdate();
            else if (args.EventId == DMSystemEventIds.OnScreenDisplayEnabledEventId)
                InputOnScreenDisplayEnabledFeedback.FireUpdate();
        }

        void TxRxPair_DMOutputChange(Switch device, DMOutputEventArgs args)
        {
            if (args.EventId == DMOutputEventIds.VideoOutEventId)
                VideoSourceFeedback.FireUpdate();
        }

        void TxRxPair_DMInputChange(Switch device, DMInputEventArgs args)
        {
            if (args.EventId == DMInputEventIds.VideoDetectedEventId)
                SyncDetectedFeedbacks[args.Number].FireUpdate();
        }

        public void AutoRouteOn()
        {
            TxRxPair.TransmitterAutoModeOn();
        }

        public void AutoRouteOff()
        {
            TxRxPair.TransmitterAutoModeOff();
        }

        public void PriorityRouteOn()
        {
            TxRxPair.PriorityRoutingOn();
        }

        public void PriorityRouteOff()
        {
            TxRxPair.PriorityRoutingOff();
        }

        public void OnScreenDisplayEnable()
        {
            TxRxPair.OnScreenDisplayEnabled();
        }

        public void OnScreenDisplayDisable()
        {
            TxRxPair.OnScreenDisplayDisabled();
        }

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            var number = Convert.ToUInt32(inputSelector); // Cast can sometimes fail

            var input = number == 0 ? null : TxRxPair.Inputs[number];

            TxRxPair.HdmiOutputs[1].VideoOut = input;
        }

        // This device has a different class for com ports which will make it hard to implement IComPorts....

        //#region IComPorts Members
        //public CrestronCollection<ComPort> ComPorts { get { return TxRxPair.ComPorts as CrestronCollection<ComPort>; } }
        //public int NumberOfComPorts { get { return 1; } }
        //#endregion
    }

    public class HdMdxxxCEPropertiesConfig
    {
        public ControlPropertiesConfig Control { get; set; }
    }
}
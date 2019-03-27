﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using PepperDash.Essentials.Devices.Common.Cameras;

namespace PepperDash.Essentials.AppServer.Messengers
{
	/// <summary>
	/// Provides a messaging bridge for a VideoCodecBase device
	/// </summary>
	public class VideoCodecBaseMessenger : MessengerBase
	{
		/// <summary>
		/// 
		/// </summary>
		public VideoCodecBase Codec { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="codec"></param>
		public VideoCodecBaseMessenger(string key, VideoCodecBase codec, string messagePath)
            : base(key, messagePath)
		{
			if (codec == null)
				throw new ArgumentNullException("codec");

			Codec = codec;
			codec.CallStatusChange += new EventHandler<CodecCallStatusItemChangeEventArgs>(codec_CallStatusChange);
			codec.IsReadyChange += new EventHandler<EventArgs>(codec_IsReadyChange);

			var dirCodec = codec as IHasDirectory;
			if (dirCodec != null)
			{
				dirCodec.DirectoryResultReturned += new EventHandler<DirectoryEventArgs>(dirCodec_DirectoryResultReturned);

			}

            var recCodec = codec as IHasCallHistory;
            if (recCodec != null)
            {
                recCodec.CallHistory.RecentCallsListHasChanged += new EventHandler<EventArgs>(CallHistory_RecentCallsListHasChanged);
            }


		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void CallHistory_RecentCallsListHasChanged(object sender, EventArgs e)
        {
            var recents = (sender as CodecCallHistory).RecentCalls;

            if (recents != null)
            {
                PostStatusMessage(new
                {
                    recentCalls = recents
                });
            }
        }
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void dirCodec_DirectoryResultReturned(object sender, DirectoryEventArgs e)
		{
            SendDirectory((Codec as IHasDirectory).CurrentDirectoryResult, e.DirectoryIsOnRoot);
		}

        /// <summary>
        /// Posts the current directory
        /// </summary>
        void SendDirectory(CodecDirectory directory, bool isRoot)
        {
            var dirCodec = Codec as IHasDirectory;

            if (dirCodec != null)
            {
                var prefixedDirectoryResults = PrefixDirectoryFolderItems(directory);

                var directoryMessage = new
                {
                    currentDirectory = new
                    {
                        directoryResults = prefixedDirectoryResults,
                        isRootDirectory = isRoot
                    }
                };
                PostStatusMessage(directoryMessage);
            }
        }

        /// <summary>
        /// Iterates a directory object and prefixes any folder items with "[+] "
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
         List<DirectoryItem> PrefixDirectoryFolderItems (CodecDirectory directory)
        {
            var tempDirectoryList = new List<DirectoryItem>();

            if (directory.CurrentDirectoryResults.Count > 0)
            {
                foreach (var item in directory.CurrentDirectoryResults)
                {
                    if (item is DirectoryFolder)
                    {
                        var newFolder = new DirectoryFolder();

                        newFolder = (DirectoryFolder)item.Clone();

                        string prefixName = "[+] " + newFolder.Name;

                        newFolder.Name = prefixName;

                        tempDirectoryList.Add(newFolder);
                    }
                    else
                    {
                        tempDirectoryList.Add(item);
                    }
                }
            }
            //else
            //{
            //    DirectoryItem noResults = new DirectoryItem() { Name = "No Results Found" };

            //    tempDirectoryList.Add(noResults);
            //}

            return tempDirectoryList;
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void codec_IsReadyChange(object sender, EventArgs e)
		{
			PostStatusMessage(new
			{
				isReady = true
			});
		}

		/// <summary>
		/// Called from base's RegisterWithAppServer method
		/// </summary>
		/// <param name="appServerController"></param>
		protected override void CustomRegisterWithAppServer(CotijaSystemController appServerController)
		{
			appServerController.AddAction("/device/videoCodec/isReady", new Action(SendIsReady));
			appServerController.AddAction("/device/videoCodec/fullStatus", new Action(SendVtcFullMessageObject));
			appServerController.AddAction("/device/videoCodec/dial", new Action<string>(s => Codec.Dial(s)));
			appServerController.AddAction("/device/videoCodec/endCallById", new Action<string>(s =>
			{
				var call = GetCallWithId(s);
				if (call != null)
					Codec.EndCall(call);
			}));
			appServerController.AddAction(MessagePath + "/endAllCalls", new Action(Codec.EndAllCalls));
			appServerController.AddAction(MessagePath + "/dtmf", new Action<string>(s => Codec.SendDtmf(s)));
			appServerController.AddAction(MessagePath + "/rejectById", new Action<string>(s =>
			{
				var call = GetCallWithId(s);
				if (call != null)
					Codec.RejectCall(call);
			}));
			appServerController.AddAction(MessagePath + "/acceptById", new Action<string>(s =>
			{
				var call = GetCallWithId(s);
				if (call != null)
					Codec.AcceptCall(call);
			}));

            // Directory actions
            var dirCodec = Codec as IHasDirectory;
            if (dirCodec != null)
            {
                appServerController.AddAction(MessagePath + "/getDirectory", new Action(GetDirectoryRoot));
                appServerController.AddAction(MessagePath + "/directoryById", new Action<string>(s => GetDirectory(s)));
                appServerController.AddAction(MessagePath + "/directorySearch", new Action<string>(s => DirectorySearch(s)));
                appServerController.AddAction(MessagePath + "/directoryBack", new Action(GetPreviousDirectory));
            }

            // History actions
            var recCodec = Codec as IHasCallHistory;
            if (recCodec != null)
            {
                appServerController.AddAction(MessagePath + "/getCallHistory", new Action(GetCallHistory));
            }

            var cameraCodec = Codec as IHasCameras;
            if (cameraCodec != null)
            {
                cameraCodec.CameraSelected += new EventHandler<CameraSelectedEventArgs>(cameraCodec_CameraSelected);

                cameraCodec.SelectedCameraFeedback.OutputChange +=new EventHandler<PepperDash.Essentials.Core.FeedbackEventArgs>(SelectedCameraFeedback_OutputChange);
                appServerController.AddAction(MessagePath + "/cameraSelect", new Action<string>(s => cameraCodec.SelectCamera(s)));

                MapCameraActions();

                var presetsCodec = Codec as IHasCameraPresets;
                if (presetsCodec != null)
                {
                    // Map preset actions
                }

                var speakerTrackCodec = Codec as IHasCameraAutoMode;
                if (speakerTrackCodec != null)
                {
                    appServerController.AddAction(MessagePath + "/cameraAuto", new Action(speakerTrackCodec.CameraAutoModeOn));
                    appServerController.AddAction(MessagePath + "/cameraManual", new Action(speakerTrackCodec.CameraAutoModeOff));
                }
            }

            var selfViewCodec = Codec as IHasCodecSelfView;

            if(selfViewCodec != null)
                appServerController.AddAction(MessagePath + "/cameraSelfView", new Action(selfViewCodec.SelfViewModeToggle));


            var layoutsCodec = Codec as IHasCodecLayouts;
           
            if (layoutsCodec != null)
                appServerController.AddAction(MessagePath + "/cameraRemoteView", new Action(layoutsCodec.LocalLayoutToggle));

			appServerController.AddAction(MessagePath + "/privacyModeOn", new Action(Codec.PrivacyModeOn));
			appServerController.AddAction(MessagePath + "/privacyModeOff", new Action(Codec.PrivacyModeOff));
			appServerController.AddAction(MessagePath + "/privacyModeToggle", new Action(Codec.PrivacyModeToggle));
			appServerController.AddAction(MessagePath + "/sharingStart", new Action(Codec.StartSharing));
			appServerController.AddAction(MessagePath + "/sharingStop", new Action(Codec.StopSharing));
			appServerController.AddAction(MessagePath + "/standbyOn", new Action(Codec.StandbyActivate));
			appServerController.AddAction(MessagePath + "/standbyOff", new Action(Codec.StandbyDeactivate));
		}

        void SelectedCameraFeedback_OutputChange(object sender, PepperDash.Essentials.Core.FeedbackEventArgs e)
        {
            PostSelectedCamera();
        }

        void cameraCodec_CameraSelected(object sender, CameraSelectedEventArgs e)
        {
            MapCameraActions();
        }

        /// <summary>
        /// Maps the camera control actions to the current selected camera on the codec
        /// </summary>
        void MapCameraActions()
        {
            var cameraCodec = Codec as IHasCameras;

            if (cameraCodec != null && cameraCodec.SelectedCamera != null)
            {

                AppServerController.RemoveAction(MessagePath + "/cameraUp");
                AppServerController.RemoveAction(MessagePath + "/cameraDown");
                AppServerController.RemoveAction(MessagePath + "/cameraLeft");
                AppServerController.RemoveAction(MessagePath + "/cameraRight");
                AppServerController.RemoveAction(MessagePath + "/cameraZoomIn");
                AppServerController.RemoveAction(MessagePath + "/cameraZoomOut");

                var camera = cameraCodec.SelectedCamera as IHasCameraPtzControl;
                if (camera != null)
                {
                    AppServerController.AddAction(MessagePath + "/cameraUp", new PressAndHoldAction(new Action<bool>(b => { if (b)camera.TiltUp(); else camera.TiltStop(); })));
                    AppServerController.AddAction(MessagePath + "/cameraDown", new PressAndHoldAction(new Action<bool>(b => { if (b)camera.TiltDown(); else camera.TiltStop(); })));
                    AppServerController.AddAction(MessagePath + "/cameraLeft", new PressAndHoldAction(new Action<bool>(b => { if (b)camera.PanLeft(); else camera.PanStop(); })));
                    AppServerController.AddAction(MessagePath + "/cameraRight", new PressAndHoldAction(new Action<bool>(b => { if (b)camera.PanRight(); else camera.PanStop(); })));
                    AppServerController.AddAction(MessagePath + "/cameraZoomIn", new PressAndHoldAction(new Action<bool>(b => { if (b)camera.ZoomIn(); else camera.ZoomStop(); })));
                    AppServerController.AddAction(MessagePath + "/cameraZoomOut", new PressAndHoldAction(new Action<bool>(b => { if (b)camera.ZoomOut(); else camera.ZoomStop(); })));

                    var focusCamera = cameraCodec as IHasCameraFocusControl;

                    AppServerController.RemoveAction(MessagePath + "/cameraAutoFocus");
                    AppServerController.RemoveAction(MessagePath + "/cameraFocusNear");
                    AppServerController.RemoveAction(MessagePath + "/CameraFocusFar");

                    if (focusCamera != null)
                    {
                        AppServerController.AddAction(MessagePath + "/cameraAutoFocus", new Action(focusCamera.TriggerAutoFocus));
                        AppServerController.AddAction(MessagePath + "/cameraFocusNear", new PressAndHoldAction(new Action<bool>(b => { if (b)focusCamera.FocusNear(); else focusCamera.FocusStop(); })));
                        AppServerController.AddAction(MessagePath + "/cameraFocusFar", new PressAndHoldAction(new Action<bool>(b => { if (b)focusCamera.FocusFar(); else focusCamera.FocusStop(); })));
                    }
                }


            }
        }

        string GetCameraMode()
        {
            string m = "";

            var speakerTrackCodec = Codec as IHasCameraAutoMode;
            if (speakerTrackCodec != null)
            {
                if (speakerTrackCodec.CameraAutoModeIsOnFeedback.BoolValue) m = "auto";
                else  m = "manual";
            }
            return m;
        }

        void GetCallHistory()
        {
            var codec = (Codec as IHasCallHistory);

            if (codec != null)
            {
                var recents = codec.CallHistory.RecentCalls;

                if (recents != null)
                {
                    PostStatusMessage(new
                    {
                        recentCalls = recents
                    });
                }
            }
        }

		public void GetFullStatusMessage()
		{

		}

		/// <summary>
		/// Helper to grab a call with string ID
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		CodecActiveCallItem GetCallWithId(string id)
		{
			return Codec.ActiveCalls.FirstOrDefault(c => c.Id == id);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="s"></param>
		void DirectorySearch(string s)
		{
			var dirCodec = Codec as IHasDirectory;
			if (dirCodec != null)
			{
				dirCodec.SearchDirectory(s);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		void GetDirectory(string id)
		{
			var dirCodec = Codec as IHasDirectory;
			if(dirCodec == null)
			{
				return;
			}
			dirCodec.GetDirectoryFolderContents(id);
		}

		/// <summary>
		/// 
		/// </summary>
		void GetDirectoryRoot()
		{
			var dirCodec = Codec as IHasDirectory;
			if (dirCodec == null)
			{
				// do something else?
				return;
			}
			if (!dirCodec.PhonebookSyncState.InitialSyncComplete)
			{
				PostStatusMessage(new
				{
					initialSyncComplete = false
				});
				return;
			}

            dirCodec.SetCurrentDirectoryToRoot();

            //PostStatusMessage(new
            //{
            //    currentDirectory = dirCodec.DirectoryRoot
            //});
		}

        /// <summary>
        /// Requests the parent folder contents
        /// </summary>
        void GetPreviousDirectory()
        {
            var dirCodec = Codec as IHasDirectory;
            if (dirCodec == null)
            {
                return;
            }

            dirCodec.GetDirectoryParentFolderContents();
        }

		/// <summary>
		/// Handler for codec changes
		/// </summary>
		void codec_CallStatusChange(object sender, CodecCallStatusItemChangeEventArgs e)
		{
			SendVtcFullMessageObject();
		}

		/// <summary>
		/// 
		/// </summary>
		void SendIsReady()
		{
			PostStatusMessage(new
			{
				isReady = Codec.IsReady
			});
		}

		/// <summary>
		/// Helper method to build call status for vtc
		/// </summary>
		/// <returns></returns>
		void SendVtcFullMessageObject()
		{
			if (!Codec.IsReady)
			{
				return;
			}

			var info = Codec.CodecInfo;
			PostStatusMessage(new
			{
				isInCall = Codec.IsInCall,
				privacyModeIsOn = Codec.PrivacyModeIsOnFeedback.BoolValue,
				sharingContentIsOn = Codec.SharingContentIsOnFeedback.BoolValue,
				sharingSource = Codec.SharingSourceFeedback.StringValue,
				standbyIsOn = Codec.StandbyIsOnFeedback.StringValue,
				calls = Codec.ActiveCalls,
				info = new
				{
					autoAnswerEnabled = info.AutoAnswerEnabled,
					e164Alias = info.E164Alias,
					h323Id = info.H323Id,
					ipAddress = info.IpAddress,
					sipPhoneNumber = info.SipPhoneNumber,
					sipURI = info.SipUri
				},
				showSelfViewByDefault = Codec.ShowSelfViewByDefault,
				hasDirectory = Codec is IHasDirectory,
                hasDirectorySearch = true,
                hasRecents = Codec is IHasCallHistory,
                hasCameras = Codec is IHasCameras,
                cameraMode = "",
                cameraSelected = (Codec as IHasCameras).SelectedCameraFeedback.StringValue
			});
		}

        /// <summary>
        /// 
        /// </summary>
        void PostCameraMode()
        {
            PostStatusMessage(new
            {
                cameraMode = GetCameraMode()
            });
        }

        void PostSelectedCamera()
        {
            PostStatusMessage(new
            {
                cameraSelected = (Codec as IHasCameras).SelectedCameraFeedback.StringValue
            });
        }
	}
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Modding;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using Satchel;
using Satchel.BetterMenus;
using sMenuButton = Satchel.BetterMenus.MenuButton;

namespace KeybindOverhaul {
    public class KeybindOverhaul: Mod, ICustomMenuMod, IGlobalSettings<GlobalSettingsClass> {
        new public string GetName() => "KeybindOverhaul";
        public override string GetVersion() => "1.0.0.0";
        public override int LoadPriority() => int.MaxValue;

        private Menu MenuRef;
        public static GlobalSettingsClass gs { get; set; } = new();

		private static MethodInfo origHCUpdate = typeof(HeroController).GetMethod("orig_Update", BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo HCLookForInput = typeof(HeroController).GetMethod("LookForInput", BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo HCLookForQueueInput = typeof(HeroController).GetMethod("LookForQueueInput", BindingFlags.NonPublic | BindingFlags.Instance);
		private ILHook ilHeroControllerUpdate, ilHeroControllerLookForInput, ilHeroControllerLookForQueueUpdated;

        public static bool jumpWasPressedLastTime_press = false;
        public static bool jumpWasPressedLastTime_release = false;
        public static bool dashWasPressedLastTime_press = false;
        public static bool dashWasPressedLastTime_release = false;
        public static bool cdashWasPressedLastTime_press = false;
        public static bool cdashWasPressedLastTime_release = false;
        public static bool dnailWasPressedLastTime_press = false;
        public static bool dnailWasPressedLastTime_release = false;
        public static bool nailWasPressedLastTime_press = false;
        public static bool nailWasPressedLastTime_release = false;
        public static bool castWasPressedLastTime_press = false;
        public static bool castWasPressedLastTime_release = false;
        public static bool qcastWasPressedLastTime_press = false;
        public static bool qcastWasPressedLastTime_release = false;

		public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects) {
			ModHooks.FinishedLoadingModsHook += lateHook;
        }

        private void lateHook() {
            On.PlayMakerFSM.OnEnable += editFsm;

			ilHeroControllerUpdate = new ILHook(origHCUpdate, heroControllerUpdateIL);
            ilHeroControllerLookForInput = new ILHook(HCLookForInput, heroControllerLookForInputIL);
            ilHeroControllerLookForQueueUpdated = new ILHook(HCLookForQueueInput, heroControllerLookForQueueInputIL);

            On.UIManager.UIGoToKeyboardMenu += editKeyboardMenu;
            
            On.HutongGames.PlayMaker.Actions.ListenForJump.OnUpdate += listenForJump;
            On.HutongGames.PlayMaker.Actions.ListenForDash.OnUpdate += listenForDash;
            On.HutongGames.PlayMaker.Actions.ListenForSuperdash.OnUpdate += listenForSuperdash;
            On.HutongGames.PlayMaker.Actions.ListenForDreamNail.OnUpdate += listenForDreamNail;
            On.HutongGames.PlayMaker.Actions.ListenForAttack.OnUpdate += listenForAttack;
            On.HutongGames.PlayMaker.Actions.ListenForCast.CheckForInput += listenForCast;
            On.HutongGames.PlayMaker.Actions.ListenForQuickCast.OnUpdate += listenForQuickCast;
        }

        private void editKeyboardMenu(On.UIManager.orig_UIGoToKeyboardMenu orig, UIManager self) {
            orig(self);
            keybindMenuTweaks();
        }

        private async void keybindMenuTweaks() {
            for(int i = 0; i < 8; i++) {
                await Task.Yield();
            }
            GameObject mappableKeys = GameObject.Find("MappableKeys");
            GameObject dash = mappableKeys.FindGameObjectInChildren("DashButton");
            Vector3 dashPos = dash.transform.position;
            dash.SetActive(false);
            GameObject cdash = mappableKeys.FindGameObjectInChildren("SDashButton");
            Vector3 cdashPos = cdash.transform.position;
            cdash.SetActive(false);
            mappableKeys.FindGameObjectInChildren("DNailButton").SetActive(false);
            mappableKeys.FindGameObjectInChildren("QCastButton").SetActive(false);
            mappableKeys.FindGameObjectInChildren("JumpButton").FindGameObjectInChildren("Text").GetComponent<Text>().text = "Menu ctrl 1 (confirm)\r\n";
            mappableKeys.FindGameObjectInChildren("AttackButton").FindGameObjectInChildren("Text").GetComponent<Text>().text = "Menu ctrl 2 (back)\r\n";
            GameObject castButton = mappableKeys.FindGameObjectInChildren("CastButton");
            castButton.transform.position = dashPos;
            castButton.FindGameObjectInChildren("Text").GetComponent<Text>().text = "Menu ctrl 3 (cancel)\r\n";
            mappableKeys.FindGameObjectInChildren("InventoryButton").transform.position = cdashPos;
        }

        private void heroControllerUpdateIL(ILContext il) {
			ILCursor cursor = new ILCursor(il).Goto(0);
			cursor.GotoNext(i => i.MatchCallvirt<HeroController>("CanNailCharge"));
			cursor.GotoPrev(i => i.Match(OpCodes.Brfalse_S));
			cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.nailArtBind.isPressed(); });
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("attack"));
            cursor.GotoNext(i => i.Match(OpCodes.Brtrue_S));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.nailBind.isPressed(); });
        }

        private void heroControllerLookForInputIL(ILContext il) {
            ILCursor cursor = new ILCursor(il).Goto(0);
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("jump"),
                            i => i.MatchCallvirt<InControl.OneAxisInputControl>("get_WasReleased"));
            cursor.GotoNext(i => i.Match(OpCodes.Brfalse_S));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.jumpBind.wasReleased("jump"); });
            cursor.GotoNext(i => i.Match(OpCodes.Brtrue_S));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.jumpBind.isPressed(); });
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("dash"));
            cursor.GotoNext(i => i.Match(OpCodes.Brtrue_S));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.dashBind.isPressed(); });
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("attack"));
            cursor.GotoNext(i => i.Match(OpCodes.Brtrue_S));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.nailBind.isPressed(); });

        }

        private void heroControllerLookForQueueInputIL(ILContext il) {
            ILCursor cursor = new ILCursor(il).Goto(0);
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("jump"));
            cursor.GotoNext(i => i.Match(OpCodes.Brfalse));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.jumpBind.wasPressed("jump"); });
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("dash"));
            cursor.GotoNext(i => i.Match(OpCodes.Brfalse_S));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.dashBind.wasPressed("dash"); });
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("attack"));
            cursor.GotoNext(i => i.Match(OpCodes.Brfalse_S));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.nailBind.wasPressed("nail"); });
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("jump"));
            cursor.GotoNext(i => i.Match(OpCodes.Brfalse));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.jumpBind.isPressed(); });
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("dash"));
            cursor.GotoNext(i => i.Match(OpCodes.Brfalse_S));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.dashBind.isPressed(); });
            cursor.GotoNext(i => i.MatchLdfld<HeroActions>("attack"));
            cursor.GotoNext(i => i.Match(OpCodes.Brfalse_S));
            cursor.EmitDelegate<Func<bool, bool>>(j => { return gs.nailBind.isPressed(); });
        }

        private void editFsm(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self) {
            if(self.gameObject.name == "Knight") {
                if(self.FsmName == "Nail Arts") {
                    FsmState moveChoice = self.GetState("Move Choice");
                    moveChoice.RemoveAction(2);
                    moveChoice.RemoveAction(1);
                    FsmEvent cancelEvent = new("CANCEL ART");
                    moveChoice.AddTransition("CANCEL ART", "Regain Control");
                    moveChoice.AddAction(new ListenForCustomBind() {
                        keybinds = gs.cycloneBind,
                        isPressed = FsmEvent.GetFsmEvent("CYCLONE"),
                        continueListening = false
                    });
                    moveChoice.AddAction(new ListenForCustomBind() {
                        keybinds = gs.greatSlashBind,
                        isPressed = FsmEvent.GetFsmEvent("GREAT SLASH"),
                        isNotPressed = cancelEvent,
                        continueListening = false
                    });
                    FsmState inactive = self.GetState("Inactive");
                    inactive.RemoveAction(0);
                    inactive.AddAction(new ListenForCustomBind() {
                        keybinds = gs.nailArtBind,
                        wasReleased = FsmEvent.GetFsmEvent("BUTTON UP"),
                        continueListening = true
                    });

                    ListenForCustomBind extendCyclone = new ListenForCustomBind() {
                        keybinds = gs.nailBind,
                        wasPressed = FsmEvent.GetFsmEvent("BUTTON DOWN"),
                        continueListening = true
                    };
                    FsmState cycloneStart = self.GetState("Cyclone Start");
                    cycloneStart.RemoveAction(4);
                    cycloneStart.AddAction(extendCyclone);
                    FsmState cycloneSpin = self.GetState("Cyclone Spin");
                    cycloneSpin.RemoveAction(5);
                    cycloneSpin.AddAction(extendCyclone);

                }
                else if(self.FsmName == "Spell Control") {
                    FsmState inactive = self.GetState("Inactive");
                    FsmBool myActiveBool = ((ListenForCast)inactive.Actions[2]).activeBool;
                    inactive.RemoveAction(2);
                    inactive.InsertAction(new ListenForCustomBind() {
                        keybinds = gs.focusBind,
                        isPressed = FsmEvent.GetFsmEvent("ALREADY DOWN"),
                        wasPressed = FsmEvent.GetFsmEvent("BUTTON DOWN"),
                        activeBool = myActiveBool,
                        continueListening = true,
                    }, 2);

                    FsmState buttonDown = self.GetState("Button Down");
                    FsmBool pressedUp = ((ListenForUp)buttonDown.Actions[1]).isPressedBool;
                    FsmBool pressedDown = ((ListenForDown)buttonDown.Actions[2]).isPressedBool;
                    buttonDown.RemoveAction(0);
                    buttonDown.InsertAction(new ListenForCustomBind() {
                        keybinds = gs.wraithsBind,
                        wasReleased = FsmEvent.GetFsmEvent("BUTTON UP"),
                        isPressedBool = pressedUp,
                        continueListening = true
                    }, 0);
                    buttonDown.RemoveAction(1);
                    buttonDown.InsertAction(new ListenForCustomBind() {
                        keybinds = gs.diveBind,
                        wasReleased = FsmEvent.GetFsmEvent("BUTTON UP"),
                        isPressedBool = pressedDown,
                        continueListening = true
                    }, 1);
                    buttonDown.RemoveAction(2);
                    buttonDown.InsertAction(new ListenForCustomBind() {
                        keybinds = gs.castBind,
                        wasReleased = FsmEvent.GetFsmEvent("BUTTON UP"),
                        continueListening = true
                    }, 2);

                    FsmState qc = self.GetState("QC");
                    FsmEvent cancelQuick = new("CANCEL QUICK");
                    qc.AddTransition("CANCEL QUICK", "Spell End");
                    qc.RemoveAction(3);
                    qc.RemoveAction(2);
                    qc.AddAction(new ListenForCustomBind() {
                        keybinds = gs.qWraithsBind,
                        isPressed = FsmEvent.GetFsmEvent("SCREAM"),
                        continueListening = false
                    });
                    qc.AddAction(new ListenForCustomBindDouble() {
                        firstKeyCombo = gs.qDiveBind,
                        secondKeyCombo = gs.qCastBind,
                        firstEvent = FsmEvent.GetFsmEvent("QUAKE"),
                        secondEvent = FsmEvent.GetFsmEvent("FIREBALL"),
                        failedEvent = cancelQuick
                    });

                    FsmState backIn = self.GetState("Back In?");
                    FsmBool backActiveBool = ((ListenForCast)backIn.Actions[0]).activeBool;
                    backIn.RemoveAction(0);
                    backIn.InsertAction(new ListenForCustomBind() {
                        keybinds = gs.focusBind,
                        isPressed = FsmEvent.GetFsmEvent("BUTTON DOWN"),
                        isNotPressed = FsmEvent.GetFsmEvent("FINISHED"),
                        activeBool = backActiveBool,
                        continueListening = false
                    }, 0);

                    ListenForCustomBind unfocusRelease = new ListenForCustomBind() {
                        keybinds = gs.focusBind,
                        wasReleased = FsmEvent.GetFsmEvent("BUTTON UP"),
                        continueListening = true
                    };
                    ListenForCustomBind unfocusReleasePress = new ListenForCustomBind() {
                        keybinds = gs.focusBind,
                        wasReleased = FsmEvent.GetFsmEvent("BUTTON UP"),
                        isNotPressed = FsmEvent.GetFsmEvent("BUTTON UP"),
                        continueListening = true
                    };
                    FsmState focusStart = self.GetState("Focus Start");
                    focusStart.RemoveAction(16);
                    focusStart.InsertAction(unfocusRelease, 16);
                    FsmState focusStartD = self.GetState("Focus Start D");
                    focusStartD.RemoveAction(10);
                    focusStartD.InsertAction(unfocusRelease, 10);
                    FsmState focusD = self.GetState("Focus D");
                    focusD.RemoveAction(6);
                    focusD.AddAction(unfocusReleasePress);
                    FsmState focus = self.GetState("Focus");
                    focus.RemoveAction(12);
                    focus.InsertAction(unfocusReleasePress, 12);
                    FsmState focusS = self.GetState("Focus S");
                    focusS.RemoveAction(11);
                    focusS.InsertAction(unfocusReleasePress, 11);
                    FsmState focusLeft = self.GetState("Focus Left");
                    focusLeft.RemoveAction(11);
                    focusLeft.InsertAction(unfocusReleasePress, 11);
                    FsmState focusRight = self.GetState("Focus Right");
                    focusRight.RemoveAction(11);
                    focusRight.InsertAction(unfocusReleasePress, 11);
                }
                else if(self.FsmName == "Dream Nail") {
                    FsmState dreamGate = self.GetState("Dream Gate?");
                    dreamGate.RemoveAction(1);
                    dreamGate.InsertAction(new ListenForCustomBind() {
                        keybinds = gs.dgateSetBind,
                        isPressed = FsmEvent.GetFsmEvent("SET"),
                        continueListening = false
                    }, 1);
                    dreamGate.RemoveAction(2);
                    dreamGate.InsertAction(new ListenForCustomBind() {
                        keybinds = gs.dgateTravelBind,
                        isPressed = FsmEvent.GetFsmEvent("WARP"),
                        continueListening = false
                    }, 2);
                    ((SendEventByName)dreamGate.Actions[3]).delay.Value = 0.01f;
                    FsmState setCharge = self.GetState("Set Charge");
                    setCharge.RemoveAction(3);
                    setCharge.InsertAction(new ListenForCustomBind() {
                        keybinds = gs.dgateSetBind,
                        isNotPressed = FsmEvent.GetFsmEvent("CANCEL"),
                        continueListening = false
                    }, 3);
                    FsmState warpCharge = self.GetState("Warp Charge");
                    warpCharge.RemoveAction(4);
                    warpCharge.InsertAction(new ListenForCustomBind() {
                        keybinds = gs.dgateTravelBind,
                        isNotPressed = FsmEvent.GetFsmEvent("CANCEL"),
                        continueListening = false
                    }, 4);
                }
            }
            orig(self);
        }

        private void listenForJump(On.HutongGames.PlayMaker.Actions.ListenForJump.orig_OnUpdate orig, ListenForJump self) {
            if(!GameManager.instance.isPaused) {
                if(gs.jumpBind.wasPressed("jump")) {
                    self.Fsm.Event(self.wasPressed);
                }
                if(gs.jumpBind.wasReleased("jump")) {
                    self.Fsm.Event(self.wasReleased);
                }
                if(gs.jumpBind.isPressed()) {
                    self.Fsm.Event(self.isPressed);
                }
                else {
                    self.Fsm.Event(self.isNotPressed);
                }
            }
        }

        private void listenForDash(On.HutongGames.PlayMaker.Actions.ListenForDash.orig_OnUpdate orig, ListenForDash self) {
            if(gs.dashBind.wasPressed("dash")) {
                self.Fsm.Event(self.wasPressed);
            }
            if(gs.dashBind.wasReleased("dash")) {
                self.Fsm.Event(self.wasReleased);
            }
            if(gs.dashBind.isPressed()) {
                self.Fsm.Event(self.isPressed);
            }
            else {
                self.Fsm.Event(self.isNotPressed);
            }
        }

        private void listenForSuperdash(On.HutongGames.PlayMaker.Actions.ListenForSuperdash.orig_OnUpdate orig, ListenForSuperdash self) {
            if(!GameManager.instance.isPaused) {
                if(gs.cdashBind.wasPressed("cdash")) {
                    self.Fsm.Event(self.wasPressed);
                }
                if(gs.cdashBind.wasReleased("cdash")) {
                    self.Fsm.Event(self.wasReleased);
                }
                if(gs.cdashBind.isPressed()) {
                    self.Fsm.Event(self.isPressed);
                }
                else {
                    self.Fsm.Event(self.isNotPressed);
                }
            }
        }

        private void listenForDreamNail(On.HutongGames.PlayMaker.Actions.ListenForDreamNail.orig_OnUpdate orig, ListenForDreamNail self) {
            customBind anyDreamNail = setupAnyBinds("dream nail");
            if(!GameManager.instance.isPaused && (self.activeBool.Value || self.activeBool.IsNone)) {
                if(anyDreamNail.wasPressed("dnail")) {
                    self.Fsm.Event(self.wasPressed);
                }
                if(anyDreamNail.wasReleased("dnail")) {
                    self.Fsm.Event(self.wasReleased);
                }
                if(anyDreamNail.isPressed()) {
                    self.Fsm.Event(self.isPressed);
                }
                else {
                    self.Fsm.Event(self.isNotPressed);
                }
            }
        }

        private void listenForAttack(On.HutongGames.PlayMaker.Actions.ListenForAttack.orig_OnUpdate orig, ListenForAttack self) {
            if(!GameManager.instance.isPaused) {
                if(gs.nailBind.wasPressed("nail")) {
                    self.Fsm.Event(self.wasPressed);
                }
                if(gs.nailBind.wasReleased("nail")) {
                    self.Fsm.Event(self.wasReleased);
                }
                if(gs.nailBind.isPressed()) {
                    self.Fsm.Event(self.isPressed);
                }
                else {
                    self.Fsm.Event(self.isNotPressed);
                }
            }
        }

        private void listenForCast(On.HutongGames.PlayMaker.Actions.ListenForCast.orig_CheckForInput orig, ListenForCast self) {
            customBind anyCast = setupAnyBinds("cast");
            if(!GameManager.instance.isPaused && (self.activeBool.Value || self.activeBool.IsNone)) {
                if(anyCast.wasPressed("cast")) {
                    self.Fsm.Event(self.wasPressed);
                }
                if(anyCast.wasReleased("cast")) {
                    self.Fsm.Event(self.wasReleased);
                }
                if(anyCast.isPressed()) {
                    self.Fsm.Event(self.isPressed);
                }
                else {
                    self.Fsm.Event(self.isNotPressed);
                }
            }
        }

        private void listenForQuickCast(On.HutongGames.PlayMaker.Actions.ListenForQuickCast.orig_OnUpdate orig, ListenForQuickCast self) {
            customBind anyQuick = setupAnyBinds("quick cast");
            if(!GameManager.instance.isPaused) {
                if(anyQuick.wasPressed("qcast")) {
                    self.Fsm.Event(self.wasPressed);
                }
                if(anyQuick.wasReleased("qcast")) {
                    self.Fsm.Event(self.wasReleased);
                }
                if(anyQuick.isPressed()) {
                    self.Fsm.Event(self.isPressed);
                }
                else {
                    self.Fsm.Event(self.isNotPressed);
                }
            }
        }

        public customBind setupAnyBinds(string which) {
            customBind newAnyBind = new(false, "any");
            switch(which) {
                case "cast":
                    gs.castBind.cloneBindsTo(newAnyBind);
                    gs.wraithsBind.cloneBindsTo(newAnyBind);
                    gs.diveBind.cloneBindsTo(newAnyBind);
                    newAnyBind.name = "anycast";
                    break;
                case "quick cast":
                    gs.qCastBind.cloneBindsTo(newAnyBind);
                    gs.qWraithsBind.cloneBindsTo(newAnyBind);
                    gs.qDiveBind.cloneBindsTo(newAnyBind);
                    newAnyBind.name = "anyquickcast";
                    break;
                case "dream nail":
                    gs.dnailBind.cloneBindsTo(newAnyBind);
                    gs.dgateSetBind.cloneBindsTo(newAnyBind);
                    gs.dgateTravelBind.cloneBindsTo(newAnyBind);
                    newAnyBind.name = "anydreamnail";
                    break;
            }
            return newAnyBind;
        }

        private void updateBindButton(string actionName, customBind bind) {
            Element elem = MenuRef.Find(actionName + " Bind");
            elem.Name = actionName + " - " + bind + "\r\n";
            elem.Update();
        }

        private async void onBindButtonPress(UnityEngine.UI.MenuButton mButton, string actionName, Ref<customBind> bind) {
            GameManager.instance.inputHandler.StopUIInput();
            Element button = MenuRef.Find(actionName + " Bind");
            button.Name = actionName + " - LISTENING\r\n";
            button.Update();
            List<KeyCode> newInput = await customBind.buildCombo();
            if(newInput != null) {
                if(newInput.Count == 1 && newInput[0] == KeyCode.Backspace) {
                    bind.Value.clearBinds();
                }
                else {
                    bind.Value.addBind(newInput);
                }
            }
            updateBindButton(actionName, bind);
            GameManager.instance.inputHandler.StartUIInput();
        }

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? modtoggledelegates) {
            MenuRef ??= new Menu(
                name: "Key Binds",
                elements: new Element[] {
                    new TextPanel(
                        name: "To clear all inputs, select the action and press BACKSPACE",
                        fontSize: 28
                    ),
                    new sMenuButton(
                        name: "Reset Defaults",
                        description: "",
                        submitAction: (Mbutton) => {
                            customBind.setDefaults();
                            updateBindButton("Jump", gs.jumpBind);
                            updateBindButton("Nail", gs.nailBind);
                            updateBindButton("Nail Arts", gs.nailArtBind);
                            updateBindButton("Cyclone", gs.cycloneBind);
                            updateBindButton("Great Slash", gs.greatSlashBind);
                            updateBindButton("Dash", gs.dashBind);
                            updateBindButton("Focus", gs.focusBind);
                            updateBindButton("Cast", gs.castBind);
                            updateBindButton("Quick Cast", gs.qCastBind);
                            updateBindButton("Wraiths", gs.wraithsBind);
                            updateBindButton("Quick Wraiths", gs.qWraithsBind);
                            updateBindButton("Dive", gs.diveBind);
                            updateBindButton("Quick Dive", gs.qDiveBind);
                            updateBindButton("Super Dash", gs.cdashBind);
                            updateBindButton("Dream Nail", gs.dnailBind);
                            updateBindButton("Set Dream Gate", gs.dgateSetBind);
                            updateBindButton("Use Dream Gate", gs.dgateTravelBind);
                        },
                        Id: "Reset"
                    ),
                    new sMenuButton(
                        name: "Jump - " + gs.jumpBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Jump", new Ref<customBind>(gs.jumpBind));
                        },
                        Id: "Jump Bind"
                    ),
                    new sMenuButton(
                        name: "Nail - " + gs.nailBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Nail", new Ref<customBind>(gs.nailBind));
                        },
                        Id: "Nail Bind"
                    ),
                    new sMenuButton(
                        name: "Nail Arts - " + gs.nailArtBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Nail Arts", new Ref<customBind>(gs.nailArtBind));
                        },
                        Id: "Nail Arts Bind"
                    ),
                    new sMenuButton(
                        name: "Cyclone - " + gs.cycloneBind,
                        description: "Held when NAIL ARTS is released",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Cyclone", new Ref<customBind>(gs.cycloneBind));
                        },
                        Id: "Cyclone Bind"
                    ),
                    new sMenuButton(
                        name: "Great Slash - " + gs.greatSlashBind,
                        description: "Held when NAIL ARTS is released",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Great Slash", new Ref<customBind>(gs.greatSlashBind));
                        },
                        Id: "Great Slash Bind"
                    ),
                    new sMenuButton(
                        name: "Dash - " + gs.dashBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Dash", new Ref<customBind>(gs.dashBind));
                        },
                        Id: "Dash Bind"
                    ),
                    new sMenuButton(
                        name: "Focus - " + gs.focusBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Focus", new Ref<customBind>(gs.focusBind));
                        },
                        Id: "Focus Bind"
                    ),
                    new sMenuButton(
                        name: "Cast - " + gs.castBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Cast", new Ref<customBind>(gs.castBind));
                        },
                        Id: "Cast Bind"
                    ),
                    new sMenuButton(
                        name: "Quick Cast - " + gs.qCastBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Quick Cast", new Ref<customBind>(gs.qCastBind));
                        },
                        Id: "Quick Cast Bind"
                    ),
                    new sMenuButton(
                        name: "Wraiths - " + gs.wraithsBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Wraiths", new Ref<customBind>(gs.wraithsBind));
                        },
                        Id: "Wraiths Bind"
                    ),
                    new sMenuButton(
                        name: "Quick Wraiths - " + gs.qWraithsBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Quick Wraiths", new Ref<customBind>(gs.qWraithsBind));
                        },
                        Id: "Quick Wraiths Bind"
                    ),
                    new sMenuButton(
                        name: "Dive - " + gs.diveBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Dive", new Ref<customBind>(gs.diveBind));
                        },
                        Id: "Dive Bind"
                    ),
                    new sMenuButton(
                        name: "Quick Dive - " + gs.qDiveBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Quick Dive", new Ref<customBind>(gs.qDiveBind));
                        },
                        Id: "Quick Dive Bind"
                    ),
                    new sMenuButton(
                        name: "Super Dash - " + gs.cdashBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Super Dash", new Ref<customBind>(gs.cdashBind));
                        },
                        Id: "Super Dash Bind"
                    ),
                    new sMenuButton(
                        name: "Dream Nail - " + gs.dnailBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Dream Nail", new Ref<customBind>(gs.dnailBind));
                        },
                        Id: "Dream Nail Bind"
                    ),
                    new sMenuButton(
                        name: "Set Dream Gate - " + gs.dgateSetBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Set Dream Gate", new Ref<customBind>(gs.dgateSetBind));
                        },
                        Id: "Set Dream Gate Bind"
                    ),
                    new sMenuButton(
                        name: "Use Dream Gate - " + gs.dgateTravelBind,
                        description: "",
                        submitAction: (Mbutton) => {
                            onBindButtonPress(Mbutton, "Use Dream Gate", new Ref<customBind>(gs.dgateTravelBind));
                        },
                        Id: "Use Dream Gate Bind"
                    )
                }
            );

            return MenuRef.GetMenuScreen(modListMenu);
        }

        public bool ToggleButtonInsideMenu {
            get;
        }

        public void OnLoadGlobal(GlobalSettingsClass s) {
            gs = s;
        }

        public GlobalSettingsClass OnSaveGlobal() {
            return gs;
        }
    }

    public class customBind {
        public List<List<KeyCode>> keyCombos;
        public bool triggerIfEmpty;
        public string name;
        
        public customBind(bool triggerIfEmpty, string name) {
            keyCombos = new();
            this.triggerIfEmpty = triggerIfEmpty;
            this.name = name;
        }

        public void addBind(List<KeyCode> combo) {
            if(combo.Equals(new List<KeyCode>() { KeyCode.Backspace })) {
                clearBinds();
                return;
            }
            if(combo != null && combo.Count >= 1) {
                keyCombos.Add(combo);
            }
        }

        public void clearBinds() {
            keyCombos.Clear();
        }

        public void cloneBindsTo(customBind target) {
            foreach(List<KeyCode> combo in keyCombos) {
                target.addBind(combo);
            }
        }

        public bool isPressed() {
            if(keyCombos.Count == 0 && triggerIfEmpty)
                return true;
            foreach(List<KeyCode> combo in keyCombos) {
                if(combo.Count < 1)
                    continue;
                bool comboActive = true;
                foreach(KeyCode key in combo) {
                    if(!Input.GetKey(key)) {
                        comboActive = false;
                        break;
                    }
                }
                if(comboActive)
                    return true;
            }
            return false;
        }

        public bool wasPressed(string useCase) {
            bool variable = false;
            switch(useCase) {
                case "jump":
                    variable = KeybindOverhaul.jumpWasPressedLastTime_press;
                    KeybindOverhaul.jumpWasPressedLastTime_press = isPressed();
                    break;
                case "dash":
                    variable = KeybindOverhaul.dashWasPressedLastTime_press;
                    KeybindOverhaul.dashWasPressedLastTime_press = isPressed();
                    break;
                case "cdash":
                    variable = KeybindOverhaul.cdashWasPressedLastTime_press;
                    KeybindOverhaul.cdashWasPressedLastTime_press = isPressed();
                    break;
                case "dnail":
                    variable = KeybindOverhaul.dnailWasPressedLastTime_press;
                    KeybindOverhaul.dnailWasPressedLastTime_press = isPressed();
                    break;
                case "nail":
                    variable = KeybindOverhaul.nailWasPressedLastTime_press;
                    KeybindOverhaul.nailWasPressedLastTime_press = isPressed();
                    break;
                case "cast":
                    variable = KeybindOverhaul.castWasPressedLastTime_press;
                    KeybindOverhaul.castWasPressedLastTime_press = isPressed();
                    break;
                case "qcast":
                    variable = KeybindOverhaul.qcastWasPressedLastTime_press;
                    KeybindOverhaul.qcastWasPressedLastTime_press = isPressed();
                    break;
            }
            if(variable) {
                return false;
            }
            else if(!isPressed()) {
                return false;
            }
            return true;
        }

        public bool wasReleased(string useCase) {
            bool variable = false;
            switch(useCase) {
                case "jump":
                    variable = KeybindOverhaul.jumpWasPressedLastTime_release;
                    KeybindOverhaul.jumpWasPressedLastTime_release = isPressed();
                    break;
                case "dash":
                    variable = KeybindOverhaul.dashWasPressedLastTime_release;
                    KeybindOverhaul.dashWasPressedLastTime_release = isPressed();
                    break;
                case "cdash":
                    variable = KeybindOverhaul.cdashWasPressedLastTime_release;
                    KeybindOverhaul.cdashWasPressedLastTime_release = isPressed();
                    break;
                case "dnail":
                    variable = KeybindOverhaul.dnailWasPressedLastTime_release;
                    KeybindOverhaul.dnailWasPressedLastTime_release = isPressed();
                    break;
                case "nail":
                    variable = KeybindOverhaul.nailWasPressedLastTime_release;
                    KeybindOverhaul.nailWasPressedLastTime_release = isPressed();
                    break;
                case "cast":
                    variable = KeybindOverhaul.castWasPressedLastTime_release;
                    KeybindOverhaul.castWasPressedLastTime_release = isPressed();
                    break;
                case "qcast":
                    variable = KeybindOverhaul.qcastWasPressedLastTime_release;
                    KeybindOverhaul.qcastWasPressedLastTime_release = isPressed();
                    break;
            }
            if(!variable) {
                return false;
            }
            else if(isPressed()) {
                return false;
            }
            return true;
        }

        public static async Task<List<KeyCode>> buildCombo() {
            List<KeyCode> combo = new();
            while(!Input.anyKey) {
                await Task.Yield();
            }
            while(true) {
                if(Input.anyKeyDown) {
                    if(Input.GetKeyDown(KeyCode.Backspace)) {
                        return new List<KeyCode>() { KeyCode.Backspace };
                    }
                    foreach(KeyCode kcode in Enum.GetValues(typeof(KeyCode))) {
                        if(Input.GetKeyDown(kcode) && !(new List<KeyCode>() { KeyCode.Escape, KeyCode.Return }).Contains(kcode)) {
                            combo.Add(kcode);
                            break;
                        }
                    }
                }
                for(int i = 0; i < combo.Count; i++) {
                    if(!Input.GetKey(combo[i])) {
                        if(i == combo.Count - 1) {
                            return combo;
                        }
                        else {
                            Modding.Logger.Log("[KeybindOverhaul] - Invalid key combination! Make sure the last key in the combo is the released first.");
                            return null;
                        }
                    }
                }
                await Task.Yield();
            }
        }

        public static void setDefaults() {
            GlobalSettingsClass gs = KeybindOverhaul.gs;
            gs.jumpBind.clearBinds();
            gs.jumpBind.addBind(new List<KeyCode>() { KeyCode.Z });
            gs.nailBind.clearBinds();
            gs.nailBind.addBind(new List<KeyCode>() { KeyCode.X });
            gs.nailArtBind.clearBinds();
            gs.nailArtBind.addBind(new List<KeyCode>() { KeyCode.X });
            gs.greatSlashBind.clearBinds();
            gs.cycloneBind.clearBinds();
            gs.cycloneBind.addBind(new List<KeyCode>() { KeyCode.UpArrow });
            gs.cycloneBind.addBind(new List<KeyCode>() { KeyCode.DownArrow });
            gs.dashBind.clearBinds();
            gs.dashBind.addBind(new List<KeyCode>() { KeyCode.C });
            gs.focusBind.clearBinds();
            gs.focusBind.addBind(new List<KeyCode>() { KeyCode.A });
            gs.castBind.clearBinds();
            gs.castBind.addBind(new List<KeyCode>() { KeyCode.A });
            gs.qCastBind.clearBinds();
            gs.qCastBind.addBind(new List<KeyCode>() { KeyCode.F });
            gs.wraithsBind.clearBinds();
            gs.wraithsBind.addBind(new List<KeyCode>() { KeyCode.UpArrow, KeyCode.A });
            gs.qWraithsBind.clearBinds();
            gs.qWraithsBind.addBind(new List<KeyCode>() { KeyCode.UpArrow, KeyCode.F });
            gs.diveBind.clearBinds();
            gs.diveBind.addBind(new List<KeyCode>() { KeyCode.DownArrow, KeyCode.A });
            gs.qDiveBind.clearBinds();
            gs.qDiveBind.addBind(new List<KeyCode>() { KeyCode.DownArrow, KeyCode.F });
            gs.cdashBind.clearBinds();
            gs.cdashBind.addBind(new List<KeyCode>() { KeyCode.S });
            gs.dnailBind.clearBinds();
            gs.dnailBind.addBind(new List<KeyCode>() { KeyCode.D });
            gs.dgateSetBind.clearBinds();
            gs.dgateSetBind.addBind(new List<KeyCode>() { KeyCode.DownArrow, KeyCode.D });
            gs.dgateTravelBind.clearBinds();
            gs.dgateTravelBind.addBind(new List<KeyCode>() { KeyCode.UpArrow, KeyCode.D });
        }

        public override string ToString() {
            if(keyCombos.Count == 0)
                return "unmapped";
            string output = "";
            foreach(List<KeyCode> combo in keyCombos) {
                foreach(KeyCode key in combo) {
                    output += key.ToString() + "+";
                }
                output = output.Substring(0, output.Length - 1) + ", ";
            }
            return output.Substring(0, output.Length - 2);
        }
    }

    public class Ref<T> {
        public Ref() {}
        public Ref(T value) { Value = value; }
        public T Value { get; set; }
        public override string ToString() {
            T value = Value;
            return value == null ? "" : value.ToString();
        }
        public static implicit operator T(Ref<T> r) { return r.Value; }
        public static implicit operator Ref<T>(T value) { return new Ref<T>(value); }
    }

    public class ListenForCustomBind: FsmStateAction {
        //generalized class for most cases
        public customBind keybinds;
        public FsmEvent wasPressed;
        public FsmEvent wasReleased;
        public FsmEvent isPressed;
        public FsmEvent isNotPressed;
        public FsmBool activeBool;
        public FsmBool isPressedBool;
        public bool continueListening;
        private bool isCurrentlyPressed;
        public override void OnEnter() {
            isCurrentlyPressed = keybinds.isPressed();
        }
        public override void OnUpdate() {
            if(!GameManager.instance.isPaused) {
                if(activeBool == null || activeBool.IsNone || activeBool.Value) {
                    if(isPressed != null && keybinds.isPressed()) {
                        if(isPressedBool != null)
                            isPressedBool.Value = true;
                        base.Fsm.Event(isPressed);
                    }
                    if(isNotPressed != null && !keybinds.isPressed()) {
                        if(isPressedBool != null)
                            isPressedBool.Value = false;
                        base.Fsm.Event(isNotPressed);
                    }
                }
                if(!continueListening) {
                    Finish();
                }
                if(wasPressed != null && keybinds.isPressed() && !isCurrentlyPressed) {
                    base.Fsm.Event(wasPressed);
                }
                if(wasReleased != null) {
                    if(!keybinds.isPressed() && isCurrentlyPressed) {
                        if(isPressedBool != null)
                            isPressedBool.Value = true;
                        base.Fsm.Event(wasReleased);
                    }
                    else {
                        if(isPressedBool != null) {
                            isPressedBool.Value = false;
                        }
                    }
                }
                isCurrentlyPressed = keybinds.isPressed();
            }
        }
    }

    public class ListenForCustomBindDouble: FsmStateAction {
        //hyperspecific class for one use case
        public customBind firstKeyCombo;
        public customBind secondKeyCombo;
        public FsmEvent firstEvent;
        public FsmEvent secondEvent;
        public FsmEvent failedEvent;
        public override void OnUpdate() {
            if(!GameManager.instance.isPaused) {
                if(firstKeyCombo.isPressed()) {
                    base.Fsm.Event(firstEvent);
                }
                if(secondKeyCombo.isPressed()) {
                    base.Fsm.Event(secondEvent);
                }
                base.Fsm.Event(failedEvent);
            }
        }
    }

    public class GlobalSettingsClass {
        public customBind jumpBind = new(false, "jump") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.Z } } };
        public customBind nailBind = new(false, "nail") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.X } } };
        public customBind nailArtBind = new(false,  "nail art") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.X } } };
        public customBind greatSlashBind = new(true, "great slash") { keyCombos = new List<List<KeyCode>>() };
        public customBind cycloneBind = new(true, "cyclone") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.UpArrow }, new List<KeyCode>() { KeyCode.DownArrow } } };
        public customBind dashBind = new(false, "dash") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.C } } };
        public customBind focusBind = new(false, "focus") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.A } } };
        public customBind castBind = new(false, "cast") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.A } } };
        public customBind qCastBind = new(false, "quick cast") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.F } } };
        public customBind wraithsBind = new(false, "wraiths") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.UpArrow, KeyCode.A } } };
        public customBind qWraithsBind = new(false, "quick wraiths") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.UpArrow, KeyCode.F } } };
        public customBind diveBind = new(false, "dive") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.DownArrow, KeyCode.A } } };
        public customBind qDiveBind = new(false, "quick dive") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.DownArrow, KeyCode.F } } };
        public customBind cdashBind = new(false, "cdash") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.S } } };
        public customBind dnailBind = new(false, "dnail") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.D } } };
        public customBind dgateSetBind = new(false, "dgate set") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.DownArrow, KeyCode.D } } };
        public customBind dgateTravelBind = new(false, "dgate travel") { keyCombos = new List<List<KeyCode>>() { new List<KeyCode>() { KeyCode.UpArrow, KeyCode.D } } };
    }
}
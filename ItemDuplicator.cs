using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimpleJSON;

namespace JustAnotherUser {
    public class ItemDuplicator : MVRScript {
        protected class AtomData {
            private string _id, _type;
            private IDictionary<string, JSONClass> _storables;

            public string id { get { return this._id; } }
            public string type { get { return this._type; } }
            public IDictionary<string, JSONClass> storables { get { return this._storables; } }

            public AtomData(Atom a) {
                this._id = a.uid;
                this._type = a.type;

                this._storables = new Dictionary<string, JSONClass>();
                foreach (JSONClass e in ItemDuplicator.GetStorablesJsonFromSave(this._id)) {
                    if (!e.HasKey("id")) continue;

                    this._storables.Add(e["id"], e);
                }
            }
        }

        private string _duplicateAtomName;
        private AtomData _duplicateAtomData; // TODO save in storable JSON

        // transform difference between the duplicateAtom and the current object
        private Vector3 _spawnPosition;
        private Vector3 _spawnRotation;

        private JSONStorableStringChooser _duplicateAtomStorable;

        // SuperController events
        private SuperController.OnAtomUIDRename onAtomRename;
        private SuperController.OnAtomRemoved onAtomRemove;

        public override void Init() {
            // plugin VaM GUI description
            pluginLabelJSON.val = "ItemDuplicator v1.1";

            // select the atom to duplicate
            this._duplicateAtomStorable = new JSONStorableStringChooser("duplicate", null, "", "Duplicate", SetNewAtom);
            RegisterStringChooser(this._duplicateAtomStorable);
            var linkPopup = CreateFilterablePopup(this._duplicateAtomStorable);
            linkPopup.popupPanelHeight = 600f;
            linkPopup.popup.onOpenPopupHandlers += () => { this._duplicateAtomStorable.choices = SuperController.singleton.GetAtoms().Select(a => a.name).Distinct().ToList(); };

            var spawn = CreateButton("Spawn now");
            spawn.height = 100;
            spawn.button.onClick.AddListener(() => StartCoroutine(SpawnAtom()));
        }


        // Runs once when plugin loads (after Init)
        protected void Start() {

            this.onAtomRename = (oldName, newName) => {
                if (oldName != this._duplicateAtomName) return;
                SetNewAtom(newName);
                this._duplicateAtomStorable.val = newName;
            };
            this.onAtomRemove = (atom) => {
                // maybe the atom is gone?
                if (SuperController.singleton.GetAtomByUid(this._duplicateAtomName) == null) StartCoroutine(SpawnAtom());
            };

            SuperController.singleton.onAtomUIDRenameHandlers += this.onAtomRename;
            SuperController.singleton.onAtomRemovedHandlers += this.onAtomRemove;
        }
        
        public void OnDestroy() {
            SuperController.singleton.onAtomUIDRenameHandlers -= this.onAtomRename;
            SuperController.singleton.onAtomRemovedHandlers -= this.onAtomRemove;
        }

        private void SetNewAtom(string name) {
            this._duplicateAtomName = name;
            Atom duplicateAtom = SuperController.singleton.GetAtomByUid(name);
            this._duplicateAtomData = new AtomData(duplicateAtom); // TODO first time not working

            // if you+difference must result in final; then final-you is difference
            this._spawnPosition = Quaternion.Inverse(containingAtom.mainController.transform.rotation)*(duplicateAtom.mainController.transform.position - containingAtom.mainController.transform.position);
            this._spawnRotation = duplicateAtom.mainController.transform.rotation.eulerAngles;
        }

        public IEnumerator SpawnAtom() {
            // does it already exists?
            Atom duplicateAtom = SuperController.singleton.GetAtomByUid(this._duplicateAtomName);
            if (duplicateAtom == null) {
                // no atom; generate a new one
                yield return SuperController.singleton.AddAtomByType(this._duplicateAtomData.type, this._duplicateAtomData.id);
                duplicateAtom = SuperController.singleton.GetAtomByUid(this._duplicateAtomName);

                if (this._duplicateAtomData.type == "CustomUnityAsset" && this._duplicateAtomData.storables["asset"] != null) {
                    // load the asset
                    JSONStorable asset = duplicateAtom.GetStorableByID("asset");
                    JSONClass assetJSON = asset.GetJSON();
                    assetJSON.Add("assetName", this._duplicateAtomData.storables["asset"]["assetName"].Value);
                    assetJSON.Add("assetUrl", this._duplicateAtomData.storables["asset"]["assetUrl"].Value);
                    asset.RestoreFromJSON(assetJSON);
                }

                // plugins?
                JSONStorable plugins;
                if ((plugins = duplicateAtom.GetStorableByID("PluginManager")) != null && this._duplicateAtomData.storables["PluginManager"] != null) {
                    JSONClass pluginsJSON = plugins.GetJSON();
                    foreach (string pluginName in this._duplicateAtomData.storables["PluginManager"]["plugins"].AsObject.Keys) {
                        pluginsJSON["plugins"].Add(pluginName, this._duplicateAtomData.storables["PluginManager"]["plugins"][pluginName].Value);
                    }
                    plugins.LateRestoreFromJSON(pluginsJSON);
                }
                // TODO add the rest of the plugins data
            }

            duplicateAtom.mainController.transform.position = containingAtom.mainController.transform.position + containingAtom.mainController.transform.rotation*this._spawnPosition;
            duplicateAtom.mainController.transform.rotation = Quaternion.Euler(containingAtom.mainController.transform.rotation.eulerAngles + this._spawnRotation);
        }

        public JSONNode GetPluginJsonFromSave() {
            JSONArray storables = ItemDuplicator.GetStorablesJsonFromSave(containingAtom.name);
            if (storables == null) return null;

            foreach (JSONNode storable in storables) {
                if (storable["id"].Value == this.storeId) {
                    return storable;
                }
            }
            return null;
        }

        // @author https://raw.githubusercontent.com/ChrisTopherTa54321/VamScripts/master/FloatMultiParamRandomizer.cs
        public static JSONArray GetStorablesJsonFromSave(string id) {
            foreach (JSONNode atoms in SuperController.singleton.loadJson["atoms"].AsArray) {
                if (atoms["id"].Value != id) continue;

                return atoms["storables"]?.AsArray;
            }

            return null;
        }
    }
}
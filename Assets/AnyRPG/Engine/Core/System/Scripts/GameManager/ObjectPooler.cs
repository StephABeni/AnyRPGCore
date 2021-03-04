using AnyRPG;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AnyRPG {
    public class ObjectPooler : MonoBehaviour {

        #region Singleton
        private static ObjectPooler instance;

        public static ObjectPooler MyInstance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<ObjectPooler>();
                }
                return instance;
            }
        }

        #endregion

        [Tooltip("If all types of objects should persist through scene changes, set this to true")]
        [SerializeField]
        private bool persistSceneChange = false;

        [Tooltip("If an object is not requested with a parent, this will be used as its parent")]
        [SerializeField]
        private GameObject defaultObjectParent = null;

        [SerializeField]
        private List<PooledObjectConfig> pooledObjectConfigs = new List<PooledObjectConfig>();

        private Dictionary<GameObject, List<GameObject>> freeObjects = new Dictionary<GameObject, List<GameObject>>();
        private Dictionary<GameObject, List<GameObject>> usedObjects = new Dictionary<GameObject, List<GameObject>>();

        private void Awake() {
            SystemEventManager.StartListening("OnLevelUnload", HandleLevelUnload);
        }

        private void Start() {
            PopulateObjectPool();
        }

        public void HandleLevelUnload(string eventName, EventParamProperties eventParamProperties) {

            // if object should persist through scene changes globally, do nothing
            if (persistSceneChange == true) {
                return;
            }

            // check individual object configs to see if they should persist through scene changes
            // remove them if not
            foreach (PooledObjectConfig pooledObjectConfig in pooledObjectConfigs) {
                if (pooledObjectConfig.PersistSceneChange == false) {
                    if (freeObjects.ContainsKey(pooledObjectConfig.PooledObject)) {
                        foreach (GameObject pooledObject in freeObjects[pooledObjectConfig.PooledObject]) {
                            Destroy(pooledObject);
                        }
                        freeObjects.Remove(pooledObjectConfig.PooledObject);
                    }
                    if (usedObjects.ContainsKey(pooledObjectConfig.PooledObject)) {
                        foreach (GameObject pooledObject in usedObjects[pooledObjectConfig.PooledObject]) {
                            Destroy(pooledObject);
                        }
                        usedObjects.Remove(pooledObjectConfig.PooledObject);
                    }
                }
            }
        }

        private void AddKeyIfNeeded(GameObject gameObjectKey) {
            if (freeObjects.ContainsKey(gameObjectKey) == false) {
                freeObjects.Add(gameObjectKey, new List<GameObject>());
            }
            if (usedObjects.ContainsKey(gameObjectKey) == false) {
                usedObjects.Add(gameObjectKey, new List<GameObject>());
            }

        }

        public void PopulateObjectPool() {
            foreach (PooledObjectConfig pooledObjectConfig in pooledObjectConfigs) {
                if (pooledObjectConfig.PreloadPool == true) {
                    AddKeyIfNeeded(pooledObjectConfig.PooledObject);
                    for (int i = 0; i < pooledObjectConfig.PreloadCount; i++) {
                        freeObjects[pooledObjectConfig.PooledObject].Add(Instantiate(pooledObjectConfig.PooledObject,
                            pooledObjectConfig.PooledObject.transform.position,
                            pooledObjectConfig.PooledObject.transform.rotation,
                            defaultObjectParent.transform));
                        freeObjects[pooledObjectConfig.PooledObject][i].SetActive(false);
                    }
                }
            }
        }

        public GameObject GetPooledObject(GameObject pooledGameObject) {
            return GetPooledObject(pooledGameObject, defaultObjectParent.transform);
        }

        public GameObject GetPooledObject(GameObject pooledGameObject, Transform parentTransform) {
            return GetPooledObject(pooledGameObject, parentTransform, false);
        }

        public GameObject GetPooledObject(GameObject pooledGameObject, Transform parentTransform, bool worldPositionStays) {
            return GetPooledObject(pooledGameObject, parentTransform.TransformPoint(pooledGameObject.transform.localPosition), pooledGameObject.transform.localRotation, parentTransform, false);
        }


        public GameObject GetPooledObject(GameObject pooledGameObject, Vector3 spawnLocation, Quaternion spawnRotation, Transform parentTransform, bool worldPositionStays = false) {
            GameObject returnValue = null;
            AddKeyIfNeeded(pooledGameObject);

            // attempt to find a free object
            if (freeObjects[pooledGameObject].Count > 0) {
                returnValue = freeObjects[pooledGameObject][0];
                returnValue.transform.SetParent(parentTransform, false);
                returnValue.transform.position = spawnLocation;
                returnValue.transform.rotation = spawnRotation;
                usedObjects[pooledGameObject].Add(freeObjects[pooledGameObject][0]);
                freeObjects[pooledGameObject].RemoveAt(0);
                returnValue.SetActive(true);
            } else {
                // there were no free objects.  check if the list is allowed to expand and instantiate if necessary
                int maxObjectCount = GetMaximumObjectCount(pooledGameObject);
                if (maxObjectCount == 0 || usedObjects[pooledGameObject].Count < maxObjectCount) {
                    //returnValue = Instantiate(pooledGameObject, spawnLocation, spawnRotation, parentTransform);
                    returnValue = Instantiate(pooledGameObject, spawnLocation, spawnRotation, parentTransform);
                    usedObjects[pooledGameObject].Add(returnValue);
                }
            }
            return returnValue;
        }

        private int GetMaximumObjectCount(GameObject pooledGameObject) {
            int returnValue = 0;
            foreach (PooledObjectConfig pooledObjectConfig in pooledObjectConfigs) {
                if (pooledObjectConfig.PooledObject = pooledGameObject) {
                    returnValue = pooledObjectConfig.MaxObjects;
                    break;
                }
            }

            return returnValue;
        }

        public void ReturnObjectToPool(GameObject pooledGameObject, float delayTime = 0f) {
            if (delayTime == 0f) {
                ReturnObjectToPool(pooledGameObject);
                return;
            }
            StartCoroutine(ReturnDelay(pooledGameObject, delayTime));
        }

        public IEnumerator ReturnDelay(GameObject pooledGameObject, float delayTime) {
            yield return new WaitForSeconds(delayTime);
            ReturnObjectToPool(pooledGameObject);
        }

        public void ReturnObjectToPool(GameObject pooledGameObject) {
            foreach (GameObject gameObjectKey in usedObjects.Keys) {
                if (usedObjects[gameObjectKey].Contains(pooledGameObject)) {
                    usedObjects[gameObjectKey].Remove(pooledGameObject);
                    freeObjects[gameObjectKey].Add(pooledGameObject);

                    break;
                }
            }

            // move pooled object to pool transform
            // that would be great if this could be done by setting active false first,
            // but Unity gives an error message that you can't set parent while deactivating
            pooledGameObject.transform.SetParent(defaultObjectParent.transform);
            pooledGameObject.SetActive(false);
        }

    }
}
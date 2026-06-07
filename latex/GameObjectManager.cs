using Amethyst_game_engine.Core.CameraModule;
using Amethyst_game_engine.Core.GameObjects;
using Amethyst_game_engine.Core.GameObjects.Components;
using OpenTK.Mathematics;
using System.Diagnostics.CodeAnalysis;

namespace Amethyst_game_engine.Core.Managers;

public sealed class GameObjectManager: IDisposable
{
    public event Action<DrawableObject>? GameObjectAdded;
    public event Action<DrawableObject>? GameObjectRemoved;
    public event Action? OnClear;

    private readonly List<DrawableObject> _gameObjects = [];
    private readonly List<string> _models = [];

    private BaseScene? _scene;

    public IReadOnlyList<DrawableObject> GameObjects => _gameObjects;
    internal IReadOnlyList<string> Models => _models;

    public int Count => _gameObjects.Count;

    [MemberNotNull(nameof(_scene))]
    internal void SetBaseScene(BaseScene scene) => _scene = scene;

    public int RemoveGameObjects(Predicate<DrawableObject> condition)
    {
        var removedModels = new HashSet<string>();

        for (int i = 0; i < _gameObjects.Count; i++)
        {
            if (condition(_gameObjects[i]))
            {
                removedModels.Add(_gameObjects[i].ModelPath);
                GameObjectRemoved?.Invoke(_gameObjects[i]);
                _gameObjects[i].Cleanup();
            }
        }

        int removedCount = _gameObjects.RemoveAll(condition);

        if (removedCount > 0)
        {
            var modelsToRemove = new List<string>();

            foreach (var modelPath in removedModels)
            {
                bool modelStillUsed = _gameObjects.Any(go => go.ModelPath == modelPath);

                if (modelStillUsed == false)
                    modelsToRemove.Add(modelPath);
            }

            foreach (var modelPath in modelsToRemove)
            {
                int removedModelPos = _models.IndexOf(modelPath);
                _models.Remove(modelPath);

                foreach (var remainingObj in _gameObjects)
                {
                    if (remainingObj.ModelIndex > removedModelPos)
                        remainingObj.ModelIndex--;
                }
            }
        }

        return removedCount;
    }

    public void AddGameObject(DrawableObject obj)
    {
        if (_scene is null)
            return;

        _gameObjects.Add(obj);
        obj.SetScene(_scene);
        obj.UpdateRenderSettings();

        if (_models.Contains(obj.ModelPath) == false)
            _models.Add(obj.ModelPath);

        obj.ModelIndex = _models.IndexOf(obj.ModelPath);

        GameObjectAdded?.Invoke(obj);
    }

    public bool RemoveGameObjectAt(int index)
    {
        if (index >= 0 && index < _gameObjects.Count)
        {
            string modelPath = _gameObjects[index].ModelPath;
            GameObjectRemoved?.Invoke(_gameObjects[index]);

            _gameObjects[index].Cleanup();
            _gameObjects.RemoveAt(index);

            bool modelStillUsed = _gameObjects.Any(go => go.ModelPath == modelPath);

            if (modelStillUsed == false)
            {
                int removedModelPos = _models.IndexOf(modelPath);
                _models.Remove(modelPath);

                foreach (var remainingObj in _gameObjects)
                {
                    if (remainingObj.ModelIndex > removedModelPos)
                        remainingObj.ModelIndex--;
                }
            }

            return true;
        }

        return false;
    }

    public bool RemoveGameObject(DrawableObject obj)
    {
        if (_gameObjects.Contains(obj))
        {
            string modelPath = obj.ModelPath;
            GameObjectRemoved?.Invoke(obj);

            obj.Cleanup();
            _gameObjects.Remove(obj);

            bool modelStillUsed = _gameObjects.Any(go => go.ModelPath == modelPath);

            if (modelStillUsed == false)
            {
                int removedModelPos = _models.IndexOf(modelPath);
                _models.Remove(modelPath);

                foreach (var remainingObj in _gameObjects)
                {
                    if (remainingObj.ModelIndex > removedModelPos)
                        remainingObj.ModelIndex--;
                }
            }

            return true;
        }

        return false;
    }

    public IEnumerable<DrawableObject> FindGameObjects(Predicate<DrawableObject> condition)
    {
        foreach (var gameObj in _gameObjects)
        {
            if (condition(gameObj))
                yield return gameObj;
        }
    }

    public DrawableObject? GetGameObjectAt(int index)
    {
        if (index >= 0 && index < _gameObjects.Count)
            return _gameObjects[index];
        else
            return null;
    }

    public DrawableObject? PickObject(float mouseX, float mouseY, int screenWidth, int screenHeight, Camera cam)
    {
        DrawableObject? closest = null;
        float closestDistance = float.MaxValue;

        Vector3 rayOrigin = cam.Position;
        Vector3 rayDirection = cam.GetPickRay(mouseX, mouseY, screenWidth, screenHeight);

        foreach (var obj in _gameObjects)
        {
            if (obj.RayIntersectsAABB(rayOrigin, rayDirection, out float distance))
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = obj;
                }
            }
        }

        return closest;
    }

    public void Clear()
    {
        Cleanup();
        _gameObjects.Clear();

        OnClear?.Invoke();
    }

    internal void Cleanup()
    {
        foreach (var gameObject in _gameObjects)
            gameObject.Cleanup();
    }

    void IDisposable.Dispose()
    {
        Cleanup();
    }
}

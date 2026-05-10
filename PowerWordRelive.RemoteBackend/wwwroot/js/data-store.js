export class DataStore {
    constructor() {
        this._data = null;
        this._listeners = [];
    }

    get refinements() {
        return this._data?.refinements || [];
    }

    get storyProgress() {
        return this._data?.story_progress || [];
    }

    get tasks() {
        return this._data?.tasks || {};
    }

    get consistency() {
        return this._data?.consistency || [];
    }

    update(data) {
        this._data = data;
        this._listeners.forEach(fn => fn(data));
    }

    onChange(fn) {
        this._listeners.push(fn);
    }
}

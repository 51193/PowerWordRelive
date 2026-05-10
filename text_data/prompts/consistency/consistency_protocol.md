输出格式：每行一条指令，格式如下：

```
consistency|append|名称|详细描述|world         新增世界观条目（向玩家展示）
consistency|append|名称|详细描述|character     新增人物条目（向玩家展示）
consistency|append|名称|详细描述|item          新增物品条目（向玩家展示）
consistency|append|名称|详细描述|event         新增事件条目（向玩家展示）
consistency|append|名称|详细描述|null           新增内部追踪条目（不向玩家展示）
consistency|edit|名称|详细描述                  编辑条目描述（仅修改详细描述）
consistency|edit_tag|名称|标签                  仅修改标签
consistency|remove|名称                         删除条目（标记删除，不物理删除记录）。
EMPTY                                           本回合无任何变化。
```

约束：
- 名称应精炼准确（人物用全名/称号，地点用名称，物品用特征名称）
- 详细描述应包含时间、地点、人物、剧情地位、潜在伏笔等完整信息
- tag 必须为 world / character / item / event / null 之一
- tag 为 null 表示内部追踪用，前端不向玩家展示。玩家角色（PC）自身相关内容必须用 null
- tag 为 world / character / item / event 时，该条目会作为"关键字笔记"向玩家展示，仅对玩家游戏过程中有笔记参考价值的 NPC、地点、物品、事件使用
- 不要输出任何解释性文字、Markdown、JSON 或格式标记
- 管道符 `|` 可能出现在名称或描述中，解析器会正确处理
- 每行只能有一条指令；无法解析的行会被静默忽略
- 如果输出 EMPTY，之后的行都将被忽略

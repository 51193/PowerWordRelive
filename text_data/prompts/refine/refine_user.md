{{dir:prompts/refine/refine_requirements.md}}

{{dir:prompts/refine/refine_protocol.md}}

{{folder:character_cards}}

本次精炼目标的上下文信息如下：

## 一致性表格
以下是游戏中已记录的人物、地点、物品等关键信息表，用于帮助你纠正专有名词和角色信息错误：
{{value:consistency_table}}

## 原始对话日志
以下是近期经过 ASR 转录的对话记录（按时间排序，最新窗口），你需要从中提取角色扮演内容进行精炼：
{{value:dialogue_window}}

## 当前精炼状态
以下是此前已经精炼好的剧本条目（编号固定快照），你需要基于此输出增删改指令：
{{value:refinement_window}}

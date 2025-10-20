import {_LS, importLanguage, registerAppSchema, registerSchema, SchemaLoadState, type IStructScalarFieldConfig } from "schema-node"
import waterFallView from "@/view/waterFallView.vue"
import { regSchemaTypeView } from "schema-node-vueview"

registerSchema([
  {
    "name": "test",
    "type": "namespace",
    "display": _LS("test"),
    "schemas": [
      {
        "name": "test.gpa",
        "type": "enum",
        "display": _LS("test.gpa"),
        "enum": {
          "type": "int",
          "values": [
            {
              "value": 4,
              "name": {
                "key": "A",
                "trans": [
                  {
                    "lang": "zhCN",
                    "tran": "优秀"
                  }
                ]
              },
              "disable": false
            },
            {
              "value": 3,
              "name": {
                "key": "B",
                "trans": [
                  {
                    "lang": "zhCN",
                    "tran": "良好"
                  }
                ]
              },
              "disable": false
            },
            {
              "value": 2,
              "name": {
                "key": "C",
                "trans": [
                  {
                    "lang": "zhCN",
                    "tran": "及格"
                  }
                ]
              },
              "disable": false
            },
            {
              "value": 1,
              "name": {
                "key": "D",
                "trans": [
                  {
                    "lang": "zhCN",
                    "tran": "不及格"
                  }
                ]
              },
              "disable": false
            }
          ]
        }
      },
      {
        "name": "test.nosubjects",
        "type": "func",
        "display": _LS("test.nosubjects"),
        "func": {
          "return": "system.bool",
          "args": [
            {
              "name": "subjects",
              "type": "test.subjects",
              "nullable": true
            }
          ],
          "exps": [
            {
              "name": "isnull",
              "return": "system.bool",
              "type": "call",
              "func": "system.logic.isnull",
              "args": [
                {
                  "name": "subjects"
                }
              ]
            },
            {
              "name": "len",
              "return": "system.int",
              "type": "call",
              "func": "system.collection.arrlen",
              "args": [
                {
                  "name": "subjects",
                  "value": "[]"
                }
              ]
            },
            {
              "name": "zerolen",
              "return": "system.bool",
              "type": "call",
              "func": "system.logic.equal",
              "args": [
                {
                  "name": "len"
                },
                {
                  "value": "0"
                }
              ]
            },
            {
              "name": "result",
              "return": "system.bool",
              "type": "call",
              "func": "system.logic.orelse",
              "args": [
                {
                  "name": "isnull"
                },
                {
                  "name": "zerolen"
                }
              ]
            }
          ]
        }
      },
      {
        "name": "test.averagefields",
        "type": "func",
        "display": _LS("test.averagefields"),
        "func": {
            "return": "system.number",
            "args": [
                {
                    "name": "array",
                    "type": "system.array",
                    "nullable": false,
                },
                {
                    "name": "field",
                    "type": "system.string",
                    "nullable": false,
                }
            ],
            "exps": [
                {
                    "name": "numbers",
                    "type": "call",
                    "func": "system.collection.getfields",
                    "return": "system.numbers",
                    "args": [
                        {
                            "name": "array",
                        },
                        {
                            "name": "field",
                        }
                    ]
                },
                {
                    name: "result",
                    type: "call",
                    func: "system.collection.average",
                    return: "system.number",
                    args: [
                        {
                            name: "numbers",
                        }
                    ]
                }
            ],
        }
      },
      {
        "name": "test.person",
        "type": "struct",
        "display": _LS("test.person"),
        "struct": {
          "fields": [
            {
              "name": "name",
              "type": "system.string",
              "display": _LS("test.person.name"),
              "upLimit": "24",
              "require": true
            } as IStructScalarFieldConfig,
            {
              "name": "subjects",
              "type": "test.subjects",
              "display": _LS("test.person.subjects"),
              "require": true
            },
            {
              "name": "gpa",
              "type": "system.bool",
              "display": _LS("test.person.gpa"),
              "default": false,
            },
            {
              "name": "scores",
              "type": "test.subjectscores",
              "display": _LS("test.person.scores"),
            },
            {
              "name": "avg",
              "type": "system.number",
              "display": _LS("test.person.avg"),
              "displayOnly": true
            }
          ],
          "relations": [
            {
              "field": "scores",
              "type": "invisible",
              "func": "test.nosubjects",
              "args": [
                {
                  "name": "subjects"
                }
              ]
            },
            {
              "field": "scores.subject",
              "type": "whiteList",
              "func": "system.conv.assign",
              "args": [
                {
                  "name": "subjects"
                }
              ]
            },
            {
              "field": "scores.subject",
              "type": "blackList",
              "func": "system.collection.getfields",
              "args": [
                {
                  "name": "scores",
                  "value": "[]"
                },
                {
                  "value": "subject"
                }
              ]
            },
            {
              "field": "scores.score",
              "type": "type",
              "func": "system.logic.cond",
              "args": [
                {
                  "name": "gpa",
                },
                {
                  "value": "test.gpa"
                },
                {
                  "value": "system.int"
                }
              ]
            },
            {
              "field": "avg",
              "type": "default",
              "func": "test.averagefields",
              "args": [
                {
                  "name": "scores"
                },
                {
                  "value": "score"
                }
              ]
            }
          ]
        }
      },
      {
        "name": "test.persons",
        "type": "array",
        "display": _LS("test.persons"),
        "array": {
          element: "test.person",
          primary: ["name"]
        }
      },
      {
        "name": "test.subject",
        "type": "enum",
        "display": _LS("test.subject"),
        "enum": {
          "type": "int",
          "cascade": [
            "category",
            "subject"
          ],
          "values": [
            {
              "value": 100,
              "name": {
                "key": "Language",
                "trans": [
                  {
                    "lang": "zhCN",
                    "tran": "语言"
                  }
                ]
              },
              "disable": false,
              "subList": [
                {
                  "value": 101,
                  "name": {
                    "key": "English",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "英语"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 102,
                  "name": {
                    "key": "Grammar and Vocabulary",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "语法"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 103,
                  "name": {
                    "key": "Speech",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "演讲"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 104,
                  "name": {
                    "key": "Creative Writing",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "写作"
                      }
                    ]
                  },
                  "disable": false
                }
              ]
            },
            {
              "value": 200,
              "name": {
                "key": "Mathematics",
                "trans": [
                  {
                    "lang": "zhCN",
                    "tran": "数学"
                  }
                ]
              },
              "disable": false,
              "subList": [
                {
                  "value": 201,
                  "name": {
                    "key": "Algebra I & II",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "代数"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 202,
                  "name": {
                    "key": "Geometry",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "几何"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 203,
                  "name": {
                    "key": "Trigonometry",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "三角学"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 204,
                  "name": {
                    "key": "Calculus",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "计算"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 205,
                  "name": {
                    "key": "Statistics",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "统计"
                      }
                    ]
                  },
                  "disable": false
                }
              ]
            },
            {
              "value": 300,
              "name": {
                "key": "Science",
                "trans": [
                  {
                    "lang": "zhCN",
                    "tran": "科学"
                  }
                ]
              },
              "disable": false,
              "subList": [
                {
                  "value": 301,
                  "name": {
                    "key": "Biology",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "生物"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 302,
                  "name": {
                    "key": "Chemistry",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "化学"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 303,
                  "name": {
                    "key": "Physics",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "物理"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 304,
                  "name": {
                    "key": "Earth Science",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "地球科学"
                      }
                    ]
                  },
                  "disable": false
                },
                {
                  "value": 305,
                  "name": {
                    "key": "Environmental Science",
                    "trans": [
                      {
                        "lang": "zhCN",
                        "tran": "环境"
                      }
                    ]
                  },
                  "disable": false
                }
              ]
            }
          ]
        }
      },
      {
        "name": "test.subjects",
        "type": "array",
        "display": _LS("test.subjects"),
        "array": {
          "element": "test.subject"
        }
      },
      {
        "name": "test.subjectscore",
        "type": "struct",
        "display": _LS("test.subjectscore"),
        "struct": {
          "fields": [
            {
              "name": "subject",
              "type": "test.subject",
              "display": _LS("test.subjectscore.subject"),
              "require": true
            },
            {
              "name": "score",
              "type": "system.int",
              "display": _LS("test.subjectscore.score"),
              "require": true,
              "lowLimit": "0",
              "upLimit": "100"
            } as IStructScalarFieldConfig
          ]
        }
      },
      {
        "name": "test.subjectscores",
        "type": "array",
        "display": _LS("test.subjectscores"),
        "array": {
          "element": "test.subjectscore",
          "primary": [
            "subject"
          ]
        }
      },
      {
        "name": "test.role",
        "type": "enum",
        "display": _LS("test.role"),
        "enum": {
          "type": "flags",
          "values": [
            {
              "value": 0,
              "name": _LS("None"),
              "disable": false
            },
            {
              "value": 1,
              "name": _LS("Worker"),
              "disable": false
            },
            {
              "value": 2,
              "name": _LS("Mananger"),
              "disable": false
            },
            {
              "value": 4,
              "name": _LS("Admin"),
              "disable": false
            }
          ]
        }
      },
      {
        "name": "test.gensubjectscore",
        "type": "func",
        "display": _LS("test.gensubjectscore"),
        "func": {
          "return": "test.subjectscore",
          "args": [
            {
              "name": "subject",
              "type": "test.subject"
            },
            {
              "name": "score",
              "type": "system.int"
            },
          ],
          "exps": []
        }
      },
      {
        "name": "test.gettelnos",
        "type": "func",
        "display": _LS("test.gettelnos"),
        "func": {
          "return": "system.strings",
          "args": [
            {
              "name": "telno",
              "type": "system.string"
            }
          ],
          "exps": [],
          "func": (telno: string) => {
            const remain = 11 - (telno?.length ?? 0)
            if (remain <= 0) return []
            const res: string[] = []
            const len = Math.pow(10, remain)
            while (res.length < 9)
            {
              const rand = `${telno}${Math.floor(Math.random() * len)}`
              if (!res.includes(rand)) res.push(rand)
            }
            return res 
          }
        }
      },
      {
        "name": "test.telno",
        "type": "scalar",
        "display": _LS("telno"),
        "scalar": {
          "base": "system.string",
          "upLimit": 11,
          "whiteList": "test.gettelnos",
          "asSuggest": true
        }
      }
    ]
  }
], SchemaLoadState.Custom)

registerAppSchema([
 {
    "name": "test",
    "display": _LS("test.app"),
    "fields": [
      {
        "name": "subjects",
        "type": "test.subjects",
        "display": _LS("test.subjects")
      },
      {
        "name": "students",
        "type": "test.persons",
        "display": _LS("test.persons")
      }
    ],
    "relations": [
      {
        "field": "students.subjects",
        "func": "system.conv.assign",
        "type": "whiteList",
        "args": [
          {
            "name": "subjects"
          }
        ]
      }
    ],
  }
])

importLanguage("enUS", {
  "test": "A test namespace",
  "test.app": "A test app",
  "test.gpa": "Grade Point Average",
  "test.nosubjects": "No subject choosed",
  "test.person": "A person info",
  "test.persons": "Person List",
  "test.subject": "Subject",
  "test.subjects": "Subjects",
  "test.subjectscore": "Subect score",
  "test.subjectscores": "Subject score list",
  "test.person.name": "Name",
  "test.person.subjects": "Subjects",
  "test.person.gpa": "Use GPA",
  "test.person.scores": "Scores",
  "test.person.avg": "Average",
  "test.subjectscore.subject": "Subject",
  "test.subjectscore.score": "Score",
  "test.role": "Role",
  "test.gensubjectscore": "Combine subject and score",
})

importLanguage("zhCN", {
  "test": "测试用",
  "test.app": "测试用应用",
  "test.gpa": "使用积点",
  "test.nosubjects": "未选中学科",
  "test.person": "学生成绩信息",
  "test.persons": "学生列表",
  "test.subject": "学科",
  "test.subjects": "学科列表",
  "test.subjectscore": "学科成绩",
  "test.subjectscores": "学科成绩列表",
  "test.person.name": "名字",
  "test.person.subjects": "学科列表",
  "test.person.gpa": "使用积点",
  "test.person.scores": "成绩列表",
  "test.person.avg": "平均成绩",
  "test.subjectscore.subject": "学科",
  "test.subjectscore.score": "成绩",
  "test.role": "角色",
  "test.gensubjectscore": "组合学科成绩",
})

regSchemaTypeView("test.persons", waterFallView, "waterfall")
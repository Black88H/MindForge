using System;
using System.Collections.Generic;

namespace MindForge.Models;

public enum MaterialFormat { PDF, DOCX, Image, Handwriting }
public enum EdgeRelationType { Prerequisite, RelatedTo, PartOf, Contradicts }
public enum LearningTaskType { Review, NewContent, Test, FeynmanCheck }
public enum ChatRole { User, Assistant }
public enum QuestionType { MultipleChoice, FillBlank, FreeText, Matching, TrueFalse }
public enum TestType { Generated, FromPhoto, Custom }
public enum Difficulty { Easy, Medium, Hard, Exam }
public enum XPSource { TestCompleted, LessonCompleted, StreakBonus, AchievementUnlocked, FeynmanPassed, MaterialUploaded }

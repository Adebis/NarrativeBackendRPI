using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dialogue_Data_Entry
{
    class Story
    {
        //A story is a list of story nodes.
        private List<StoryNode> story_sequence;
        private int anchor_node_id;
        public int current_turn;

        public Story(int anchor_node_id_in)
        {
            current_turn = 0;
            anchor_node_id = anchor_node_id_in;
            story_sequence = new List<StoryNode>();
        }//end method Story
        public Story(List<StoryNode> base_story_sequence, int anchor_node_id_in)
        {
            current_turn = 0;
            anchor_node_id = anchor_node_id_in;
            story_sequence = new List<StoryNode>();
            StoryNode temp_node = null;
            foreach (StoryNode base_node in base_story_sequence)
            {
                //Make a deep copy of the base node.
                temp_node = new StoryNode(base_node.graph_node_id);
                temp_node.turn = base_node.turn;
                temp_node.text = base_node.text;

                foreach (Tuple<string, int> story_act in base_node.story_acts)
                {
                    temp_node.AddStoryAct(story_act.Item1, story_act.Item2);
                }//end foreach
                AddStoryNode(temp_node);
            }//end foreach
        }//end method Story

        public void AddStoryNode(StoryNode new_story_node)
        {
            new_story_node.turn = current_turn;
            story_sequence.Add(new_story_node);
            current_turn += 1;
        }//end method AddStoryNode

        //Get the story as a history list of feature ids
        public List<int> GetHistory()
        {
            List<int> return_list = new List<int>();

            foreach (StoryNode temp_node in story_sequence)
            {
                return_list.Add(temp_node.graph_node_id);
            }//end foreach

            return return_list;
        }//end method GetHistory

        //Return the index of the last segment of the story, starting after the
        //last story node with a user turn which is not the last node
        //of the story sequence.
        public int GetLastSegmentIndex()
        {
            int last_user_turn_index = -1;
            for (int i = story_sequence.Count - 1; i >= 0; i--)
            {
                if (story_sequence[i].HasStoryAct(Constant.USERTURN) && (i != story_sequence.Count - 1))
                {
                    last_user_turn_index = i;
                    break;
                }//end if
            }//end for

            //The last segment of the story starts the turn after the last user turn.
            return last_user_turn_index + 1;
        }//end mehtod GetLastSegment
        public int GetSecondToLastSegmentIndex()
        {
            int second_to_last_user_turn_index = -1;
            //Search from before the start of the last segment.
            for (int i = GetLastSegmentIndex() - 1; i >= 0; i--)
            {
                if (story_sequence[i].HasStoryAct(Constant.USERTURN) && (i != story_sequence.Count - 1))
                {
                    second_to_last_user_turn_index = i;
                    break;
                }//end if
            }//end for

            return second_to_last_user_turn_index;
        }//end method GetSecondToLastSegmentIndex
        //Return the last segment.
        public List<StoryNode> GetLastSegment()
        {
            List<StoryNode> last_segment = new List<StoryNode>();
            for (int i = GetLastSegmentIndex(); i < StorySequence.Count; i++)
            {
                last_segment.Add(StorySequence[i]);
            }//end for
            return last_segment;
        }//end method GetLastSegment
        public List<StoryNode> GetRemainderStory()
        {
            List<StoryNode> remainder_story = new List<StoryNode>();
            for (int i = 0; i < GetLastSegmentIndex(); i++)
            {
                remainder_story.Add(StorySequence[i]);
            }//end for
            return remainder_story;
        }//end method GetRemainderStory
        public List<StoryNode> GetSecondToLastSegment()
        {
            List<StoryNode> second_to_last_segment = new List<StoryNode>();
            if (GetSecondToLastSegmentIndex() != -1)
            {
                for (int i = GetSecondToLastSegmentIndex(); i < GetLastSegmentIndex(); i++)
                {
                    second_to_last_segment.Add(StorySequence[i]);
                }//end for
            }//end if
            return second_to_last_segment;
        }//end method GetSecondToLastSegment

        public int AnchorNodeId
        {
            get
            {
                return this.anchor_node_id;
            }//end get
            set
            {
                this.anchor_node_id = value;
            }//end set
        }
        public List<StoryNode> StorySequence
        {
            get
            {
                return this.story_sequence;
            }//end get
        }
    }
}

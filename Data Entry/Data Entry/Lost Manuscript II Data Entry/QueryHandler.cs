using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dialogue_Data_Entry;
using AIMLbot;
using System.Collections;
using System.Diagnostics;
using Newtonsoft.Json;

using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace Dialogue_Data_Entry
{
	enum Direction : int
	{
		NORTH = 1, SOUTH = -1,
		EAST = 2, WEST = -2,
		NORTHEAST = 3, SOUTHWEST = -3,
		NORTHWEST = 4, SOUTHEAST = -4,
		CONTAIN = 5, INSIDE = -5,
		HOSTED = 6, WAS_HOSTED_AT = -6,
		WON = 0
	}
	enum Question : int
	{
		WHAT = 0, WHERE = 1, WHEN = 2
	}

	/// <summary>
	/// A data structure to hold information about a query
	/// </summary>
	class Query
	{
		// The name of the Feature that the user asked about
		public Feature MainTopic { get; set; }
		// Whether or not the input was an explicit question
		public bool IsQuestion { get { return QuestionType != null; } }
		// The type of Question
		public Question? QuestionType { get; private set; }
		// The direction/relationship asked about.
		public Direction? Direction { get; private set; }
		public string DirectionWord { get; private set; }
		public bool HasDirection { get { return Direction != null; } }

		public Query(Feature mainTopic, Question? questionType, Direction? directions, string direction_word = "")
		{
			MainTopic = mainTopic;
			QuestionType = questionType;
			Direction = directions;
			DirectionWord = direction_word;
		}
		public override string ToString()
		{
			string s = "Topic: " + MainTopic.Id;
			s += "\nQuestion type: " + QuestionType ?? "none";
			s += "\nDirection specified: " + Direction ?? "none";
			s += "\nDirection word: " + DirectionWord ?? "none";
			return s;
		}
	}

	/// <summary>
	/// A utility class to parse natural input into a Query and a Query into natural output.
	/// </summary>
	class QueryHandler
	{
		private const string FORMAT = "FORMAT:";
		private const string IDK = "I'm afraid I don't know anything about that topic." + "##" + "对不起，我不知道。" + "##";
		private string[] punctuation = { ",", ";", ".", "?", "!", "\'", "\"", "(", ")", "-" };
		private string[] questionWords = { "?", "what", "where", "when" };

		private string[] directionWords = {"inside", "contain", "north", "east", "west", "south",
									  "northeast", "northwest", "southeast", "southwest",
									  "hosted", "was_hosted_at", "won"};

		private string[] Directional_Words = { "is southwest of", "is southeast of"
				, "is northeast of", "is north of", "is west of", "is east of", "is south of", "is northwest of" };

		//Related to spatial constraint. Relationships that can be used to describe the location of something.
		private string[] locational_words = { "is north of", "is northwest of", "is east of", "is south of"
												, "is in", "is southwest of", "is west of", "is northeast of"
												, "is southeast of", "took place at", "was held by"
												, "was partially held by" };

		
		// "is in" -> contains?
		private int iterations;
		private Bot bot;
		private User user;
		private FeatureGraph graph;

		private List<string> features;

		private int noveltyAmount = 5;
		private List<TemporalConstraint> temporalConstraintList;
		//private List<int> topicHistory = new List<int>();
		private string prevSpatial;

		private NarrationManager narration_manager;

		public LinkedList<Feature> prevCurr = new LinkedList<Feature>();

		//A list of all the features that have been chosen as main topics
		public LinkedList<Feature> feature_history = new LinkedList<Feature>();
		//The topic before the current one
		public Feature previous_topic;

		public int countFocusNode = 0;
		public double noveltyValue = 0.0;

        public Form1 parent_form1;

		//A list of string lists, each of which represents a set of relationship
		//words which may be interchangeable when used to find analogies.
		public List<List<string>> equivalent_relationships = new List<List<string>>();

		//FILTERING:
		//A list of nodes to filter out of mention.
		//Nodes in this list won't be spoken explicitly unless they
		//are directly queried for.
		//These nodes are still included in traversals, but upon traveling to
		//one of these nodes the next step in the traversal is automatically taken.
		public List<string> filter_nodes = new List<string>();
		//A list of relationships which should not be used for analogies.
		public List<String> no_analogy_relationships = new List<string>();

		//JOINT MENTIONS:
		//A list of feature lists, each of which represent
		//nodes that should be mentioned together
		public List<List<Feature>> joint_mention_sets = new List<List<Feature>>();

		//Which language we are operating in.
		//Default is English.
		public int language_mode_display = Constant.EnglishMode;
		public int language_mode_tts = Constant.EnglishMode;

		//A string to be used for text-to-speech
		public string buffered_tts = "";

        public Story main_story;
        public List<Story> stories;

		/// <summary>
		/// Create a converter for the specified XML file
		/// </summary>
		/// <param name="xmlFilename"></param>
		public QueryHandler(FeatureGraph graph, List<TemporalConstraint> myTemporalConstraintList, Form1 parent_f1)
		{
            parent_form1 = parent_f1;
			// Load the AIML Bot
			//this.bot = new Bot();
			this.temporalConstraintList = myTemporalConstraintList;
			/*bot.loadSettings();
			bot.isAcceptingUserInput = false;
			bot.loadAIMLFromFiles();
			bot.isAcceptingUserInput = true;
			this.user = new User("user", this.bot);*/

			// Load the Feature Graph
			this.graph = graph;

			this.iterations = 0;

			// Feature Names, with which to index the graph
			this.features = graph.getFeatureNames();

			//Initialize the dialogue manager
			narration_manager = new NarrationManager(this.graph, myTemporalConstraintList);

            main_story = null;
            stories = new List<Story>();

			//Build lists of equivalent relationships
			//is, are, was, is a kind of, is a
			equivalent_relationships.Add(new List<string>() { "is", "are", "was", "is a kind of", "is a" });
			//was a member of, is a member of
			equivalent_relationships.Add(new List<string>() { "was a member of", "is a member of" });
			//won a gold medal in, won
			equivalent_relationships.Add(new List<string>() { "won a gold medal in", "won" });
			//is one of, was one of the, was one of
			equivalent_relationships.Add(new List<string>() { "is one of", "was one of the", "was one of" });
			//include, includes, included
			equivalent_relationships.Add(new List<string>() { "include", "includes", "included" });
			//took place on
			equivalent_relationships.Add(new List<string>() { "took place on" });
			//took place at
			equivalent_relationships.Add(new List<string>() { "took place at" });
			//has, had
			equivalent_relationships.Add(new List<string>() { "has", "had" });
			//includes event
			equivalent_relationships.Add(new List<string>() { "includes event" });
			//includes member, included member
			equivalent_relationships.Add(new List<string>() { "includes member", "included member" });
			//include athlete
			equivalent_relationships.Add(new List<string>() { "include athlete" });
			//is southwest of, is southeast of, is northeast of, is north of,
			//is west of, is east of, is south of, is northwest of
			equivalent_relationships.Add(new List<string>() { "is southwest of", "is southeast of"
				, "is northeast of", "is north of", "is west of", "is east of", "is south of", "is northwest of" });

			//Build list of filter nodes.
			//Each filter node is identified by its Data values in the XML
			filter_nodes.Add("Male");
			filter_nodes.Add("Female");
			filter_nodes.Add("Cities");
			filter_nodes.Add("Sports");
			filter_nodes.Add("Gold Medallists");
			filter_nodes.Add("Venues");
			filter_nodes.Add("Time");
			filter_nodes.Add("Aug. 8th, 2008");
			filter_nodes.Add("Aug. 24th, 2008");
			filter_nodes.Add("Aug. 9th, 2008");
			filter_nodes.Add("Aug. 10th, 2008");
			filter_nodes.Add("Aug. 11th, 2008");
			filter_nodes.Add("Aug. 12th, 2008");
			filter_nodes.Add("Aug. 13th, 2008");
			filter_nodes.Add("Aug. 14th, 2008");
			filter_nodes.Add("Aug. 15th, 2008");
			filter_nodes.Add("Aug. 16th, 2008");
			filter_nodes.Add("Aug. 17th, 2008");
			filter_nodes.Add("Aug. 18th, 2008");
			filter_nodes.Add("Aug. 19th, 2008");
			filter_nodes.Add("Aug. 20th, 2008");
			filter_nodes.Add("Aug. 21st, 2008");
			filter_nodes.Add("Aug. 22nd, 2008");
			filter_nodes.Add("Aug. 23rd, 2008");


			//Build list of relationships which should not be used in analogies.
			no_analogy_relationships.Add("occurred before");
			no_analogy_relationships.Add("occurred after");
			no_analogy_relationships.Add("include");
			no_analogy_relationships.Add("includes");
			no_analogy_relationships.Add("included");
			no_analogy_relationships.Add("has");
			no_analogy_relationships.Add("had");
		}//end constructor QueryHandler
			
		private string MessageToServer(Feature feat, string speak, string noveltyInfo, string proximalInfo = "", bool forLog = false, bool out_of_topic_response = false)
		{
			String return_message = "";

			String to_speak = speak; //SpeakWithAdornments(feat, speak);

			//Add adjacent node info to the end of the message.
			//
			//to_speak += AdjacentNodeInfo(feat, last);

			if (out_of_topic_response)
			{
				//"I'm afraid I don't know anything about ";
				to_speak = "I'm sorry, I'm afraid I don't understand what you are asking. But here's something I do know about. "
				   + "##" + "对不起，我不知道您在说什么。但我知道这些。" + "##" + to_speak;
			}//end if

			string tts = ParseOutput(to_speak, language_mode_tts);
			buffered_tts = tts;
			to_speak = ParseOutput(to_speak, language_mode_display);

			if (forLog)
				return_message = to_speak + "\r\n";
			else
			{
				return_message = " ID:" + feat.Id + ":Speak:" + to_speak + ":Novelty:" + noveltyInfo + ":Proximal:" + proximalInfo;
				//return_message += "##" + tts;
			}//end else
				

			//Console.WriteLine("to_speak: " + to_speak);

			return return_message;
		}//end function MessageToServer

        private string MakeAnalogy(int id_1, int id_2)
        {
            string analogy_string = "";
            string feature_name_1 = graph.getFeature(id_1).Name;
            string feature_name_2 = graph.getFeature(id_2).Name;

            try
            {
                using (var client = new HttpClient())
                {
                    string url = "http://localhost:5000/get_analogy";
                    //string url = "http://storytelling.hass.rpi.edu:5000/get_analogy";
                    //string url_parameters = "?file=" + file_name;

                    client.BaseAddress = new Uri(url);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    Dictionary<string, string> content = new Dictionary<string, string>
                    {
                        {"file1", graph.file_name},
                        {"file2", graph.file_name},
                        {"feature1", feature_name_1},
                        {"feature2", feature_name_2}
                    };

                    var http_content = new FormUrlEncodedContent(content);
                    HttpResponseMessage response = client.PostAsync(url, http_content).Result;

                    //Read the jsons tring from the http response
                    Task<string> read_string_task = response.Content.ReadAsStringAsync();
                    read_string_task.Wait(100000);

                    string content_string = read_string_task.Result;
                    analogy_string = content_string;
                }//end using
            }//end try
            catch (Exception e)
            {
                Console.WriteLine("Error contacting analogy server: " + e.Message);
            }//end catch

            return analogy_string;
        }//end function MakeAnalogy

        bool json_mode = true;
        bool user_interest_mode = false;
		//Form2 calls this function
		//input is the input to be parsed.
		//messageToServer indicates whether or not we are preparing a response to the front-end.
		//forLog indicates whether or not we are preparing a response for a log output.
		//outOfTopic indicates whether or not we are continuing out-of-topic handling.
		//projectAsTopic true means we use forward projection to choose the next node to traverse to based on
		//  how well the nodes in the n-length path from the current node relate to the current node.
		public string ParseInput(string input, bool messageToServer = false, bool forLog = false, bool outOfTopic = false, bool projectAsTopic = false)
		{
            int story_section_size = 3;
            string return_string = "";

            // Respond to any commands.
            String[] split_input = input.Trim().Split(':');
            return_string = ParseCommand(split_input);
            // If there was a command in the input, ParseCommand would have
            // returned a non-empty string. If so, return the string as the response.
            if (!return_string.Equals("")) 
            {
                return return_string;
            }//end if

            //Next, try to start or continue the chronology.
            //Check for feature names in input.
            Feature input_feature = FindFeature(input);
            if (input_feature != null)
            {
                //If we have found a feature, then there is an explicitly requested anchor node for the next storyline.
                //Generate the next section of the chronology.
                input = "CHRONOLOGY:" + input + ":" + story_section_size;
            }//end if
            //Otherwise, pass in the empty string as input to the chronology command.
            //This will get the default next best story node.
            else
            {
                input = "CHRONOLOGY:" + "" + ":" + story_section_size;
            }//end else

            split_input = input.Trim().Split(':');
            return_string = ParseCommand(split_input);

            return return_string;
		}//end ParseInput

		//PARSE INPUT UTILITY FUNCTIONS
		/// <summary>
		/// ParseInput utility function. Looks for an explicit command word in the given input and tries to carry
		/// out the command. Returns the result of the command if any valid command is found.
		/// </summary>
		/// <param name="split_input">A string of input split into an array by the character ":"</param>
		private string ParseCommand(string[] split_input)
		{
            string json_string = "";

            if (split_input[0].ToLower().Equals("chronology"))
            {
                // Command takes two inputs:
                // 1. Node name for chronology
                // 2. Turn limit for chronology
                // Check the inputs.
                string topic = "";
                int turn_limit = 5;
                if (split_input.Length > 1 && split_input[1] != null)
                {
                    topic = split_input[1];
                }//end if
                if (split_input.Length > 2 && split_input[2] != null)
                {
                    int temp = 0;
                    bool parse_success = int.TryParse(split_input[2], out temp);
                    if (parse_success)
                    {
                        turn_limit = temp;
                        Console.Out.WriteLine("Turn limit set to " + temp);
                    }//end if
                    else
                        Console.Out.WriteLine("Could not set turn limit.");
                }//end if

                return Chronology(topic, turn_limit);
            }//end if
            //Make a story using a list of nodes, identified by node ID or node name.
            if (split_input[0].ToLower().Equals("make_story"))
            {
                if (split_input.Length > 1)
                {
                    List<string> input_list = new List<string>();
                    for (int i = 1; i < split_input.Length - 1; i++)
                        input_list.Add(split_input[i]);
                    return MakeStory(input_list);
                }//end if
            }//end if
            //List the anchor node of each story in the list of stories
            if (split_input[0].ToLower().Equals("list_stories"))
            {
                json_string = "Stories: ";
                int story_index = 0;
                foreach (Story list_story in stories)
                {
                    json_string += "Story[" + story_index + "]=" + graph.getFeature(list_story.StorySequence[0].anchor_node_id).Name + "\n ";
                    story_index += 1;
                }//end foreach
            }//end if
            //Read a story from the list of stories, by index.
            if (split_input[0].ToLower().Equals("read_story"))
            {
                json_string = "";
                int input_int = -1;
                bool parse_success = int.TryParse(split_input[1], out input_int);
                if (parse_success && stories.Count > input_int)
                {
                    FeatureGraph temp_graph = DeepClone.DeepCopy<FeatureGraph>(graph);
                    Story to_read = stories[input_int];
                    SpeakTransform temp_transform = new SpeakTransform(temp_graph);
                    json_string = temp_transform.SpeakStorySegment(to_read.GetLastSegment());
                }//end if
                else
                    json_string = "No valid story at given index.";
            }//end if
            //Interweave two stories from the list of stories, by index.
            if (split_input[0].ToLower().Equals("interweave_stories"))
            {
                json_string = "";
                int index_1 = -1;
                int index_2 = -1;
                bool parse_success = int.TryParse(split_input[1], out index_1);
                if (parse_success)
                {
                    parse_success = int.TryParse(split_input[2], out index_2);
                }//end if
                if (parse_success)
                {
                    Story story_1 = stories[index_1];
                    Story story_2 = stories[index_2];

                    FeatureGraph temp_graph = DeepClone.DeepCopy<FeatureGraph>(graph);
                    //Find the turn in the first storyline where the switch point should occur
                    NarrationManager temp_manager = new NarrationManager(temp_graph, temporalConstraintList);

                    Story interwoven_story = temp_manager.Interweave(story_1, story_2);

                    SpeakTransform t = new SpeakTransform(graph);
                    json_string = t.SpeakStory(interwoven_story);
                    stories.Add(interwoven_story);

                    if (json_mode)
                        json_string = JsonConvert.SerializeObject(interwoven_story);
                }//end if
                else
                    json_string = "No valid story at given indices.";
            }//end if
            //Load an XML by file name
            else if (split_input[0].ToLower().Equals("load_xml"))
            {
                string filename = "";
                if (split_input.Count() > 1)
                {
                    filename = split_input[1];
                    //load_xml = true;
                    //xml_to_load = filename;
                    return "load_xml:" + filename;
                }//end if
                else
                {
                    json_string = "No file specified.";
                }
            }//end else if
            else if (split_input[0].ToLower().Equals("analogy"))
            {
                int id_1 = -1;
                bool success = int.TryParse(split_input[1], out id_1);
                int id_2 = -1;
                success = int.TryParse(split_input[2], out id_2);

                json_string = MakeAnalogy(id_1, id_2);
            }//end else if
            //Toggle JSON response outputs on or off.
            else if (split_input[0].ToLower().Equals("toggle_json"))
            {
                if (json_mode)
                {
                    json_mode = false;
                    json_string = "JSON responses toggled off";
                }//end if
                else
                {
                    json_mode = true;
                    json_string = "JSON responses toggled on";
                }//end elses
            }//end else if
            //Toggle user interest mode on or off.
            else if (split_input[0].ToLower().Equals("toggle_user_interest"))
            {
                if (user_interest_mode)
                {
                    user_interest_mode = false;
                    json_string = "User interest mode toggled off";
                }//end if
                else
                {
                    user_interest_mode = true;
                    json_string = "User interest mode toggled on";
                }//end elses
            }//end else if
            //Reset both the main story and the feature graph.
            else if (split_input[0].ToLower().Equals("restart_narration"))
            {
                main_story = null;
                graph.ResetNodes();
                json_string = "narration restarted";
            }//end else if
            //GRAPH_INFO command.
            //List information about the knowledge graph.
            else if (split_input[0].ToLower().Equals("graph_info"))
            {
                //Count the total number of edges in the graph.
                //Count pairs of forward and backward edges as one.

                bool verbose = false;
                if (split_input.Count() > 1)
                    if (split_input[1].ToLower().Equals("verbose"))
                        verbose = true;

                //Nodes that have already been checked
                List<Feature> features_checked = new List<Feature>();
                //Relationships that have been seen
                List<string> relationships = new List<string>();
                int connection_count = 0;
                foreach (Feature feat_to_check in graph.Features)
                {
                    features_checked.Add(feat_to_check);
                    foreach (Tuple<Feature, double, string> temp_neighbor in feat_to_check.Neighbors)
                    {
                        //If this neighbor has already been checked, don't count the connection.
                        if (features_checked.Contains(temp_neighbor.Item1))
                        {
                            continue;
                        }//end if
                        //If the relationship has not been seen, add it to the list of relationships
                        if (!relationships.Contains(temp_neighbor.Item3))
                        {
                            relationships.Add(temp_neighbor.Item3);
                        }//end if
                        connection_count += 1;
                    }//end foreach
                }//end foreach

                json_string = "Number of edges: " + connection_count + ", unique relationships: " + relationships.Count;

                if (verbose)
                {
                    json_string += " Relationships: \n";

                    foreach (string relationship in relationships)
                    {
                        json_string += " (" + relationship + ") \n";
                    }//end foreach
                }//end if

                List<Feature> characters = new List<Feature>();
                List<Feature> locations = new List<Feature>();
                List<Feature> events = new List<Feature>();
                List<Feature> uncategorized = new List<Feature>();

                bool categorized = false;
                foreach (Feature temp_feature in graph.Features)
                {
                    categorized = false;
                    if (temp_feature.entity_type.Contains(Constant.CHARACTER))
                    {
                        characters.Add(temp_feature);
                        categorized = true;
                    }//end if
                    if (temp_feature.entity_type.Contains(Constant.LOCATION))
                    {
                        locations.Add(temp_feature);
                        categorized = true;
                    }//end if
                    if (temp_feature.entity_type.Contains(Constant.EVENT))
                    {
                        events.Add(temp_feature);
                        categorized = true;
                    }//end if
                    if (!categorized)
                        uncategorized.Add(temp_feature);
                }//end foreach

                int emperor_count = 0;
                foreach (Feature character in characters)
                {
                    if (character.HasEntityType("emperor"))
                        emperor_count += 1;
                }//end foreach
                json_string += " Characters (" + characters.Count + ") - emperors (" + emperor_count + "): \n";

                if (verbose)
                    foreach (Feature character in characters)
                    {
                        if (character.HasEntityType("emperor"))
                            json_string += "[emperor]";
                        json_string += " [C] " + character.Name + " \n";
                    }//end foreach

                int capital_count = 0;
                foreach (Feature location in locations)
                {
                    if (location.HasEntityType("capital"))
                        capital_count += 1;
                }//end foreach
                json_string += " Locations (" + locations.Count + ") - capitals (" + capital_count + "): \n";

                if (verbose)
                    foreach (Feature location in locations)
                    {
                        if (location.HasEntityType("capital"))
                            json_string += "[capital]";
                        json_string += " [L] " + location.Name + " \n";
                    }//end foreach

                int battle_count = 0;
                foreach (Feature temp_event in events)
                {
                    if (temp_event.HasEntityType("battle"))
                        battle_count += 1;
                }//end foreach
                json_string += " Events (" + events.Count + ") - battles (" + battle_count + "): \n";

                if (verbose)
                    foreach (Feature temp_event in events)
                    {
                        if (temp_event.HasEntityType("battle"))
                            json_string += "[battle]";
                        json_string += " [E] " + temp_event.Name + " \n";
                    }//end foreach

                json_string += " Uncategorized (" + uncategorized.Count + "): \n";

                if (verbose)
                    foreach (Feature temp_uncategorized in uncategorized)
                    {
                        json_string += " [U] " + temp_uncategorized.Name + " \n";
                    }//end foreach
            }//end else if
            //get_graph command
            else if (split_input[0].ToLower().Equals("get_graph"))
            {
                GraphLight temp_graph = new GraphLight(graph);
                if (json_mode)
                    json_string = JsonConvert.SerializeObject(temp_graph);
                else
                    json_string = JsonConvert.SerializeObject(temp_graph);
            }//end else if
            //set_anchors command.
            // Add a set of anchor nodes to the narration manager by either feature name or ID.
            else if (split_input[0].ToLower().Equals("set_anchors"))
            {
                Feature new_anchor_node = null;
                json_string = "Added anchor nodes: ";

                for (int i = 1; i < split_input.Length; i++)
                {
                    String string_topic = split_input[i];
                    //Try to convert the topic to an int to check if it's an id.
                    int int_topic = -1;
                    bool parse_success = int.TryParse(string_topic, out int_topic);
                    if (parse_success)
                    {
                        //Check that the new integer topic is a valid id.
                        new_anchor_node = graph.getFeature(int_topic);
                    }//end if
                    else
                    {
                        new_anchor_node = FindFeature(string_topic);
                    }//end else
                    if (new_anchor_node != null)
                    {
                        narration_manager.AddAnchorNode(new_anchor_node);
                        json_string += new_anchor_node.Name + " (" + new_anchor_node.Id + ")" + ", ";
                    }//end if

                }//end for

            }//end else if
            //LIST_ANCHORS command.
            //  Returns the list of anchor nodes, by name, to the chat window.
            else if (split_input[0].ToLower().Equals("list_anchors"))
            {
                json_string = "Anchor nodes: ";
                foreach (Feature anchor_node in narration_manager.anchor_nodes)
                {
                    json_string += anchor_node.Name += " (" + anchor_node.Id + "), ";
                }//end foreach
            }//end else if
            //analogical_story command
            else if (split_input[0].Equals("analogical_story"))
            {
                Feature anchor_node = null;
                //Get the anchor node specified in this command 
                if (split_input[1] != null)
                {
                    String string_topic = split_input[1];
                    //First, check if the topic is the empty string.
                    //If so, try the "default" anchor node.
                    if (string_topic.Equals(""))
                    {
                        //If there is not yet a story, get the root node as the anchor node.
                        if (main_story == null)
                        {
                            anchor_node = graph.Root;
                        }//end if
                        //If there is an ongoing story, get the next best topic based on the story.
                        else
                        {
                            anchor_node = narration_manager.getNextBestStoryTopic(main_story);
                        }//end else
                    }//end if
                    else
                    {
                        //Try to convert the topic to an int to check if it's an id.
                        int int_topic = -1;
                        bool parse_success = int.TryParse(string_topic, out int_topic);
                        if (parse_success)
                        {
                            //Check that the new integer topic is a valid id.
                            anchor_node = graph.getFeature(int_topic);
                        }//end if
                        else
                        {
                            anchor_node = FindFeature(string_topic);
                        }//end else
                    }//end else
                    if (anchor_node != null)
                    {
                        //If we found an anchor node with this command, assemble the chronology.

                        //Get the turn limit
                        int turn_limit = 0;
                        if (split_input[2] != null)
                        {
                            bool parse_success = int.TryParse(split_input[2], out turn_limit);
                            if (parse_success)
                            {
                                Console.Out.WriteLine("Turn limit set to " + turn_limit);
                            }//end if
                            else
                                Console.Out.WriteLine("Could not set turn limit.");
                        }//end if

                        //Make a temporary graph to create the chronology's order before presenting it.
                        FeatureGraph temp_graph = DeepClone.DeepCopy<FeatureGraph>(graph);

                        json_string = "";

                        NarrationManager temp_manager = new NarrationManager(graph, temporalConstraintList);

                    }//end if

                }//end else if
            }//end if

            return json_string;
		}//end function CommandResponse

        private string Chronology(string topic, int turn_limit)
        {
            string return_string = "";

            Feature anchor_node = null;
            //Get the anchor node specified in this command 
            String string_topic = topic;
            //First, check if the topic is the empty string.
            //If so, try the "default" anchor node.
            if (string_topic.Equals(""))
            {
                //If there is not yet a story, get the root node as the anchor node.
                if (main_story == null)
                {
                    anchor_node = graph.Root;
                }//end if
                //If there is an ongoing story, get the next best topic based on the story.
                else
                {
                    anchor_node = narration_manager.getNextBestStoryTopic(main_story);
                }//end else
            }//end if
            else
            {
                //Try to convert the topic to an int to check if it's an id.
                int int_topic = -1;
                bool parse_success = int.TryParse(string_topic, out int_topic);
                if (parse_success)
                {
                    //Check that the new integer topic is a valid id.
                    anchor_node = graph.getFeature(int_topic);
                }//end if
                else
                {
                    anchor_node = FindFeature(string_topic);
                }//end else
            }//end else
            if (anchor_node != null)
            {
                //If we found an anchor node, check to see if there's an existing story. If not, make a new one.
                if (main_story == null)
                {
                    return_string = "";

                    NarrationManager temp_manager = new NarrationManager(graph, temporalConstraintList);
                    Story chronology = temp_manager.GenerateChronology(anchor_node, turn_limit);

                    if (json_mode)
                        return_string = JsonConvert.SerializeObject(chronology);
                    else
                    {
                        return_string = JsonConvert.SerializeObject(chronology);
                    }//end else

                    main_story = chronology;
                }//end if
                else
                {
                    // Continue the existing story
                    NarrationManager temp_manager = new NarrationManager(graph, temporalConstraintList);
                    Story chronology = temp_manager.GenerateChronology(anchor_node, turn_limit, starting_story: main_story, user_story: true);
                    if (json_mode)
                        return_string = JsonConvert.SerializeObject(chronology);
                    else
                    {
                        return_string = JsonConvert.SerializeObject(chronology);
                    }//end else
                }//end else
            }//end if

            return return_string;
        }//end function Chronology

        private string MakeStory(List<string> feature_name_list)
        {
            Feature anchor_node = null;
            string return_string = "";
            List<Feature> input_features = new List<Feature>();
            //Resolve the node IDs or names, and get the list of their corresponding features.
            foreach (string item in feature_name_list)
            {
                int input_id = -1;
                bool parse_success = int.TryParse(item, out input_id);
                if (parse_success)
                    input_features.Add(graph.getFeature(input_id));
                else
                    input_features.Add(FindFeature(item));
            }//end foreach
            if (input_features.Count > 0)
                anchor_node = input_features[0];
            if (anchor_node != null)
            {
                //Assemble the story.
                FeatureGraph temp_graph = DeepClone.DeepCopy<FeatureGraph>(graph);

                return_string = "";

                NarrationManager temp_manager = new NarrationManager(temp_graph, temporalConstraintList);

                Story result_story = temp_manager.MakeStoryFromList(input_features);

                stories.Add(result_story);

                if (json_mode)
                    return_string = JsonConvert.SerializeObject(result_story);
                else
                {
                    return_string = JsonConvert.SerializeObject(result_story);
                }//end else
            }//end if

            return return_string;
        }//end method MakeStory


		//END OF PARSE INPUT UTILITY FUNCTIONS

		/// <summary>
		/// Convert a regular string to a Query object,
		/// identifying the MainTopic and any question and direction words
		/// </summary>
		/// <param name="input">A string of input, asking about a topic</param>
		/// <returns>A Query object that can be passed to ParseQuery for output.</returns>
		public Query BuildQuery(string input)
		{
			//DEBUG
			Console.Out.WriteLine("Building query from: " + input);
			//END DEBUG

			string mainTopic;
			Question? questionType = null;
			Direction? directionType = null;
			string directionWord = "";

			// Find the main topic!
			Feature f = FindFeature(input);
			if (f == null)
			{
				//MessageBox.Show("FindFeature returned null for input: " + input);
				return null;
			}

			this.iterations++;

			//update interest profile based on analogy

			Console.Out.WriteLine("Start handling interest");
			this.graph.update_interest_analogy(f.Id, this.iterations);
			
			//narration_manager.Topic = f;
			mainTopic = f.Name;
			if (string.IsNullOrEmpty(mainTopic))
			{
				//MessageBox.Show("mainTopic IsNullOrEmpty");
				return null;
			}

			//DEBUG
			Console.Out.WriteLine("Topic of query: " + mainTopic);
			//END DEBUG

			// Is the input a question?
			if (input.Contains("where"))
			{
				//DEBUG
				Console.Out.WriteLine("Where question");
				//END DEBUG
				questionType = Question.WHERE;
				//if (input.Contains("was_hosted_at"))
				//{
				//    directionType = Direction.WAS_HOSTED_AT;
				//}
			}
			else if (input.Contains("when"))
			{
				questionType = Question.WHEN;
			}
			else if (input.Contains("what") || input.Contains("?"))
			{
				//DEBUG
				Console.Out.WriteLine("What question");
				//END DEBUG
				questionType = Question.WHAT;

				// Check for direction words
				//if (input.Contains("direction"))
				//{
					foreach (string direction in directionWords)
					{
						// Ideally only one direction is specified
						if (input.Contains(direction))
						{
							directionType = (Direction)Enum.Parse(typeof(Direction), direction, true);
							directionWord = direction;
							// Don't break. If "northwest" is asked, "north" will match first
							// but then get replaced by "northwest" (and so on).
						}//end if
					}//end foreach

					//DEBUG
				if (directionType != null)
					Console.Out.WriteLine("Input contained direction: " + directionType.ToString());
					//END DEBUG

				//}//end if
			}//end else if
			else
			{
				int t = input.IndexOf("tell"), m = input.IndexOf("me"), a = input.IndexOf("about");
				if (0 <= t && t < m && m < a)
				{
					// "Tell me about" in that order, with any words or so in between
					// TODO:  Anything?  Should just talk about the topic, then.
				}//end if
			}//end else
			return new Query(f, questionType, directionType, directionWord);
		}//end function BuildQuery

		private string PadPunctuation(string s)
		{
			foreach (string p in punctuation)
			{
				s = s.Replace(p, " " + p);
			}//end foreach
			return s;
		}//end function PadPunctuation
		private string RemovePunctuation(string s)
		{
			foreach (string p in punctuation)
			{
				s = s.Replace(p, "");
			}
			string[] s0 = s.Split(' ');
			return string.Join(" ", s0);
		}//end function RemovePunctuation

		//Identifies the feature in the given input
		/// <summary>
		/// Takes a string and identifies which
		/// feature, if any, appears in it. Returns the feature.
		/// </summary>
		/// <param name="input">A string for the function to look for a feature in.</param>
		private Feature FindFeature(string input)
		{
			Feature target = null;
			int targetLen = 0;
			input = input.ToLower();
			foreach (string item in this.features)
			{
				string parse_item = item;
				parse_item = parse_item.Split(new string[] { "##" }, StringSplitOptions.None)[0];
				if (input.Contains(RemovePunctuation(parse_item.ToLower())))
				{
					if (parse_item.Length > targetLen)
					{
						target = this.graph.getFeature(item);
						targetLen = target.Name.Length;
					}
				}
				/*
				// original
				if (input.Contains(RemovePunctuation(item.ToLower())))
				{
					if (item.Length > targetLen)
					{
						target = this.graph.getFeature(item);
						targetLen = target.Id.Length;
					}
				}
				*/
			}
			//If the target is still null, check for 'that' or 'this'
			if (input.Contains("this") || input.Contains("that") || input.Contains("it") || input.Contains("something"))
				target = narration_manager.Topic;

			return target;
		}//end function FindFeature

		//Parses a bilingual output based on the language_mode passed in
		public string ParseOutput(string to_parse, int language_mode)
		{
			string answer = "";
			string[] answers = to_parse.Split(new string[] { "##" }, StringSplitOptions.None);

			for (int i = 0; i < answers.Length; i++)
			{
				if (language_mode == Constant.EnglishMode && i % 2 == 0)
				{
					answer += answers[i];
				}
				if (language_mode == Constant.ChineseMode && i % 2 == 1)
				{
					answer += answers[i];
				}
			}
			return answer;
		}

		private string[] FindSpeak(Feature feature)
		{
			return feature.Speaks.ToArray();
		}//end function FindSpeak

	}//end class QueryHandler

	static class ExtensionMethods
	{
		public static Direction Invert(this Direction d)
		{
			return (Direction)(-(int)d);
		}

		public static string ToUpperFirst(this string s)
		{
			return s.Substring(0, 1).ToUpper() + s.Substring(1);
		}

		public static string JoinAnd(this List<string> items)
		{
			switch (items.Count())
			{
				case 0:
					return "";
				case 1:
					return items.ElementAt(0);
				case 2:
					return items.ElementAt(0) + " and " + items.ElementAt(1);
				default:
					return string.Join(", ", items.GetRange(0, items.Count - 1))
						+ ", and " + items[items.Count - 1];
			}
		}
	}
}

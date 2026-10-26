export default class ProcessStatusRequest implements IWebApiRequest {
	private operationName: string;
	private entity: any;
	private flag: boolean;
	private count: number;
	private new_names: string[];

	constructor(private flag: boolean, private count: number, private new_names: string[], entity: any) {
		this.operationName = "new\"update\\status";
		this.flag = flag;
		this.count = count;
		this.new_names = new_names;
		this.entity = entity;
	}
	getInternalRequest(): IWebApiInternalRequest {
		return {
			entity: this.entity,
			flag: this.flag,
			count: this.count,
			new_names: this.new_names,
			getMetadata: () => ({
				boundParameter: "entity",
				parameterTypes: {
					entity: {
						typeName: "mscrm.account",
						structuralProperty: WebApiRequestStructuralProperty.EntityType
					},
					flag: {
						typeName: "Edm.Boolean",
						structuralProperty: WebApiRequestStructuralProperty.PrimitiveType
					},
					count: {
						typeName: "Edm.Int32",
						structuralProperty: WebApiRequestStructuralProperty.PrimitiveType
					},
					new_names: {
						typeName: "Collection(Edm.String)",
						structuralProperty: WebApiRequestStructuralProperty.Collection
					},
				},
				operationType: WebApiRequestOperationType.Action,
				operationName: this.operationName
			})
		} as IWebApiInternalRequest;
	}
}
